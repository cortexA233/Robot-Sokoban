"""Author inputs for v0.3.0; writes editor recipes, never runtime assets.

The offline model helps find/check routes. RecipeImporter and the real C# rules
must replay these before they can become shipped levels.
"""
import json
import sys
from collections import deque
from pathlib import Path

sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding="utf-8")
from ValidateRedirectorTeachingDrafts import Board, DIRECTIONS, ARROWS, ROOT


def definition(id, title, rows, briefing, gates=None):
    result = dict(schemaVersion=3, id=id, title=title, briefing=briefing,
                  completionText="运输区已恢复供电", width=len(rows[0]), height=len(rows),
                  gridSize=1.0, terrainRows=[], playerSpawn=None, crates=[], sockets=[],
                  gates=[], decorations=[], redirectors=[])
    for row, line in enumerate(rows):
        assert len(line) == result["width"], (id, line, len(line))
        terrain = ""
        for x, char in enumerate(line):
            z = len(rows) - row - 1
            if char == "P":
                result["playerSpawn"] = dict(x=x, z=z, facing="N")
            elif char in "ABXY":
                result["crates"].append(dict(id="crate_" + char, x=x, z=z, kind="Cargo" if char in "XY" else "Energy"))
            elif char in "abs":
                result["sockets"].append(dict(id="socket_" + char, x=x, z=z, isGoal=char != "s"))
            elif char in "DF":
                result["gates"].append(dict(id="gate_" + char, x=x, z=z, facing="N", powerMode="Any", sourceSocketIds=["socket_" + s for s in gates[char]]))
            elif char in ARROWS:
                result["redirectors"].append(dict(id="redirector_" + str(len(result["redirectors"]) + 1), x=x, z=z, facing=ARROWS[char]))
            terrain += char if char in ".#~_" else "."
        result["terrainRows"].append(terrain)
    return result


def model(d):
    cell = lambda e: [e["x"], e["z"]]
    rows = [list(row) for row in d["terrainRows"]]
    arrow = {value: key for key, value in ARROWS.items()}
    for r in d["redirectors"]:
        rows[d["height"] - 1 - r["z"]][r["x"]] = arrow[r["facing"]]
    return Board(dict(terrainRows=["".join(row) for row in rows], player=cell(d["playerSpawn"]),
        crates=[dict(cell=cell(c), kind=c["kind"]) for c in d["crates"]],
        sockets=[dict(id=s["id"], cell=cell(s), goal=s["isGoal"]) for s in d["sockets"]],
        gates=[dict(cell=cell(g), mode=g["powerMode"], sources=g["sourceSocketIds"]) for g in d["gates"]]))


def search(board, limit=250000, frozen=()):
    # Enumerate pushes; each edge includes a legal shortest walk to the push side.
    pending = deque([board.initial]); parents = {board.initial: None}
    while pending:
        state = pending.popleft()
        if board.complete(state):
            route = ""
            while parents[state] is not None:
                state, commands = parents[state]
                route = commands + route
            return dict(solvable=True, commands=route, states=len(parents), exhausted=False)
        player, crates = state
        reachable = {player: ""}; walking = deque([player])
        while walking:
            p = walking.popleft()
            for direction, (dx, dz) in DIRECTIONS.items():
                q = (p[0] + dx, p[1] + dz)
                if q not in reachable and q not in crates and board.passable(q, p, crates):
                    reachable[q] = reachable[p] + direction; walking.append(q)
        for index, crate in enumerate(crates):
            if index in frozen: continue
            for direction, (dx, dz) in DIRECTIONS.items():
                stance = (crate[0] - dx, crate[1] - dz)
                if stance not in reachable: continue
                result = board.step((stance, crates), direction)
                if result is None or not result[1] or result[0] in parents: continue
                parents[result[0]] = (state, reachable[stance] + direction)
                pending.append(result[0])
        if len(parents) > limit:
            return dict(solvable=None, states=len(parents), exhausted=False, limit=limit)
    return dict(solvable=False, states=len(parents), exhausted=True)


def studies():
    d = []
    teaching = json.loads(Path(__file__).with_name("RedirectorTeachingDrafts.json").read_text(encoding="utf-8"))
    for index, study in enumerate(teaching["levels"], 7):
        d.append(definition(f"L{index:02}", study["title"], study["rows"],
            "箱子会沿箭头转向；进入普通地板会停下，机器人需要绕到它后面。" if index == 7 else
            "先借普通箱挡停能源箱，再清走它。停稳的箱子可以从另一侧推走。"))
    d.append(definition("L09", "借电发车", [
        "########", "######a#", "######b#", "#...##D#",
        "#.B#>~.#", "#.s.A#.#", "#P.....#", "########"],
        "先把一只能源箱放进辅助插槽。箱子在门格停靠后，还要再推到深处接替供电。", {"D": "sa"}))
    d.append(definition("L10", "共用转角", [
        "########", "#...X.a#", "###.B#b#", "#...~#D#", "#...>~.#",
        "#...~#.#", "#.sA.#.#", "#P.....#", "########"],
        "两路箱子共用一个转角。留出候车与推动位置，先送深处，再回收临时电源。", {"D": "sa"}))
    d.append(definition("L11", "刹车要回来", [
        "#########", "######b##", "#...BDv.#", "#...X...#", "#PA~^####",
        "####.####", "####.####", "####a####", "#########"],
        "同一只普通箱要当两次刹车。第一个目标会打开第二段运输线，别把刹车箱提前推入死角。", {"D": "a"}))
    last = definition("L12", "最后一班", [
        "#########", "#a#######", "#.#######", "#.#....##", "#..>XF^b#",
        "#.#~D#~##", "#.#A..B##", "#.PY....#", "#########"],
        "临时电源先维持发车门；交付首箱后再开放回收路。普通箱一个让路，一个要重复挡停。", {"D": "sa", "F": "a"})
    last["sockets"].append(dict(id="socket_s", x=6, z=2, isGoal=False))
    last["completionText"] = "所有运输区已恢复供电"
    d.append(last)
    # Gate facing is visual, but line its aperture up with the actual crossing.
    d[4]["gates"][0]["facing"] = "E"
    next(g for g in last["gates"] if g["id"] == "gate_F")["facing"] = "E"
    return d


def main():
    selected = {arg for arg in sys.argv[1:] if not arg.startswith("--")}
    reports = []
    mechanics = []
    for d in studies():
        if selected and d["id"] not in selected: continue
        board = model(d)
        result = search(board)
        print(d["id"], json.dumps(result), flush=True)
        if result["solvable"] is not True:
            raise RuntimeError("No verified route for " + d["id"] + ": " + json.dumps(result))
        replay = board.replay(result["commands"])
        assert d["width"] <= 9 and d["height"] <= 9
        path = ROOT / "Docs" / "LevelRecipes" / (d["id"] + ".json")
        path.write_text(json.dumps(dict(definition=d, commands=result["commands"]), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        reports.append(dict(id=d["id"], moves=replay["moves"], pushes=replay["pushes"], searchStates=result["states"], commands=result["commands"]))
        if "--checks" in sys.argv:
            checks = dict(id=d["id"], cargoFrozen={}, gatesClosed={})
            for index, crate in enumerate(d["crates"]):
                if crate["kind"] == "Cargo": checks["cargoFrozen"][crate["id"]] = search(model(d), frozen=(index,))
            for gate in d["gates"]:
                changed = model(d)
                for candidate in changed.gates:
                    if candidate["cell"] == [gate["x"], gate["z"]]: candidate["sources"] = []
                checks["gatesClosed"][gate["id"]] = search(changed)
            if any(s["id"] == "socket_s" for s in d["sockets"]):
                changed = model(d)
                for gate in changed.gates: gate["sources"] = [s for s in gate["sources"] if s != "socket_s"]
                checks["utilityDisconnected"] = search(changed)
            for check in list(checks["cargoFrozen"].values()) + list(checks["gatesClosed"].values()) + ([checks["utilityDisconnected"]] if "utilityDisconnected" in checks else []):
                assert check["solvable"] is False and check["exhausted"], (d["id"], check)
            mechanics.append(checks)
    if mechanics:
        output = ROOT / "Docs/Versions/V0.3.0Validation/LevelMechanicChecks.json"
        output.parent.mkdir(exist_ok=True)
        output.write_text(json.dumps(dict(scope="Offline design analysis; C# replay is verified separately in Unity", levels=mechanics), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(reports, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
