"""Offline design model, NOT the Unity kernel or a runtime level importer.

Run from any directory: python <path-to-this-file>
Replays retained L04-L06 recipes as calibration, then two proposed teaching
studies. Exhaustive searches are small, local design checks, not a game feature.
"""

import json
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DIRECTIONS = {"N": (0, 1), "E": (1, 0), "S": (0, -1), "W": (-1, 0)}
ARROWS = {"^": "N", ">": "E", "v": "S", "<": "W"}


class Board:
    def __init__(self, definition):
        self.tiles = {
            (x, len(definition["terrainRows"]) - 1 - row): char
            for row, line in enumerate(definition["terrainRows"])
            for x, char in enumerate(line)
        }
        self.player = tuple(definition["player"])
        self.crates = tuple(tuple(c["cell"]) for c in definition["crates"])
        self.kinds = tuple(c["kind"] for c in definition["crates"])
        self.sockets = definition.get("sockets", [])
        self.gates = definition.get("gates", [])
        self.initial = (self.player, self.crates)

    def complete(self, state):
        energy = {c for c, k in zip(state[1], self.kinds) if k == "Energy"}
        return all(tuple(s["cell"]) in energy for s in self.sockets if s["goal"])

    def passable(self, cell, player, crates):
        if self.tiles.get(cell, "#") not in ".~^>v<":
            return False
        energy = {c for c, k in zip(crates, self.kinds) if k == "Energy"}
        powered = {s["id"]: tuple(s["cell"]) in energy for s in self.sockets}
        for gate in self.gates:
            if tuple(gate["cell"]) == cell:
                combine = all if gate["mode"] == "All" else any
                return cell == player or cell in crates or combine(powered[s] for s in gate["sources"])
        return True

    def step(self, state, direction, freeze_cargo=False):
        if self.complete(state):
            return None
        player, old_crates = state
        dx, dz = DIRECTIONS[direction]
        next_player = (player[0] + dx, player[1] + dz)
        if not self.passable(next_player, player, old_crates):
            return None
        crates = list(old_crates)
        if next_player not in crates:
            return ((next_player, tuple(crates)), 0, [])
        index = crates.index(next_player)
        if freeze_cargo and self.kinds[index] == "Cargo":
            return None
        target = (next_player[0] + dx, next_player[1] + dz)
        if target in crates or target == player or not self.passable(target, player, crates):
            return None
        crates[index] = target
        route = [target]
        visited = set()
        while self.tiles[target] in "~^>v<":
            if self.tiles[target] in ARROWS:
                dx, dz = DIRECTIONS[ARROWS[self.tiles[target]]]
            key = (target, dx, dz)
            if key in visited:
                return None  # Proposed rule: reject the entire cyclic command.
            visited.add(key)
            dest = (target[0] + dx, target[1] + dz)
            if dest == next_player or dest in crates or not self.passable(dest, next_player, crates):
                break
            target = dest
            crates[index] = target
            route.append(target)
        return ((next_player, tuple(crates)), 1, route)

    def replay(self, commands):
        state, pushes, snapshots = self.initial, 0, []
        for number, direction in enumerate(commands, 1):
            result = self.step(state, direction)
            if result is None:
                raise ValueError(f"Rejected command {number}: {direction}")
            state, pushed, route = result
            pushes += pushed
            if route:
                snapshots.append({"command": number, "route": route, "player": state[0], "crates": state[1]})
        assert self.complete(state), "Reference route did not solve the study"
        return {"moves": len(commands), "pushes": pushes, "pushSnapshots": snapshots}

    def search(self, freeze_cargo=False):
        queue = deque([self.initial])
        seen = {self.initial}
        parents = {}
        while queue:
            state = queue.popleft()
            if self.complete(state):
                commands = ""
                while state in parents:
                    state, direction = parents[state]
                    commands = direction + commands
                return {"solvable": True, "visitedStates": len(seen), "exhausted": False, "commands": commands}
            for direction in DIRECTIONS:
                result = self.step(state, direction, freeze_cargo)
                if result is not None and result[0] not in seen:
                    seen.add(result[0])
                    parents[result[0]] = (state, direction)
                    queue.append(result[0])
        return {"solvable": False, "visitedStates": len(seen), "exhausted": True}


def teaching_board(study):
    rows = study["rows"]
    assert len(rows) == study["height"] and all(len(row) == study["width"] for row in rows)
    definition = {"terrainRows": [], "crates": [], "sockets": []}
    for row, line in enumerate(rows):
        terrain = ""
        for x, char in enumerate(line):
            cell = [x, len(rows) - 1 - row]
            if char == "P":
                definition["player"] = cell
            elif char in "AX":
                definition["crates"].append({"cell": cell, "kind": "Energy" if char == "A" else "Cargo"})
            elif char == "a":
                definition["sockets"].append({"id": "a", "cell": cell, "goal": True})
            terrain += "." if char in "PAXa" else char
        definition["terrainRows"].append(terrain)
    return Board(definition)


def calibration(level_id):
    recipe = json.loads((ROOT / "Docs" / "LevelRecipes" / f"{level_id}.json").read_text(encoding="utf-8-sig"))
    d = recipe["definition"]
    cell = lambda e: [e["x"], e["z"]]
    board = Board({
        "terrainRows": d["terrainRows"], "player": cell(d["playerSpawn"]),
        "crates": [{"cell": cell(c), "kind": c.get("kind", "Energy")} for c in d["crates"]],
        "sockets": [{"id": s["id"], "cell": cell(s), "goal": s["isGoal"]} for s in d["sockets"]],
        "gates": [{"cell": cell(g), "mode": g["powerMode"], "sources": g["sourceSocketIds"]} for g in d["gates"]],
    })
    result = board.replay(recipe["commands"])
    return {"id": level_id, "moves": result["moves"], "pushes": result["pushes"]}


def main():
    data = json.loads(Path(__file__).with_name("RedirectorTeachingDrafts.json").read_text(encoding="utf-8"))
    report = {"scope": "Offline design model only; no Unity verification", "calibration": [], "studies": []}
    for level_id, expected in [("L04", (22, 8)), ("L05", (42, 13)), ("L06", (41, 15))]:
        result = calibration(level_id)
        assert (result["moves"], result["pushes"]) == expected
        report["calibration"].append(result)
    for study in data["levels"]:
        board = teaching_board(study)
        result = board.replay(study["commands"])
        assert result["moves"] == study["expectedMoves"] and result["pushes"] == study["expectedPushes"]
        removed = teaching_board(study)
        removed.tiles = {cell: "." if char in ARROWS else char for cell, char in removed.tiles.items()}
        no_redirector = removed.search()
        if study["id"] == "L07_study":
            assert not no_redirector["solvable"], (study["id"], no_redirector)
        record = {"id": study["id"], "replay": result, "redirectorReplacedByFloor": no_redirector}
        if "Cargo" in board.kinds:
            frozen = board.search(freeze_cargo=True)
            assert not frozen["solvable"]
            removed = teaching_board(study)
            removed.crates = tuple(c for c, k in zip(removed.crates, removed.kinds) if k != "Cargo")
            removed.kinds = tuple(k for k in removed.kinds if k != "Cargo")
            removed.initial = (removed.player, removed.crates)
            missing = removed.search()
            assert not missing["solvable"]
            early = teaching_board(study)
            state = early.initial
            for direction in "NEEE":
                result = early.step(state, direction)
                assert result is not None
                state = result[0]
            early.initial = state
            cleared_first = early.search()
            assert not cleared_first["solvable"]
            record.update(cargoFrozen=frozen, cargoRemoved=missing, cargoClearedFirst=cleared_first)
        report["studies"].append(record)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
