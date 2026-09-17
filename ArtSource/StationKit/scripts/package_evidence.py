"""Encode locally rendered evidence. Requires ffmpeg and Pillow, never downloads media."""
from pathlib import Path
import subprocess, shutil, json
from PIL import Image, ImageDraw, ImageFont
BASE=Path(__file__).resolve().parents[1]; PROJECT=BASE.parents[1]

def main():
    ffmpeg=shutil.which('ffmpeg'); ffprobe=shutil.which('ffprobe')
    if not ffmpeg or not ffprobe: raise RuntimeError('Put ffmpeg and ffprobe on PATH.')
    report=[]
    for name,fps,count in [('blender_mechanical',10,34),('blender_occupancy',12,72),('unity_occupancy',12,72)]:
        folder=PROJECT/'Logs/StationKitFrames'/name
        assert all((folder/f'{i:04d}.png').exists() for i in range(count)),name
        dest=BASE/'previews'/(name+'.mp4')
        subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-framerate',str(fps),'-i',str(folder/'%04d.png'),'-frames:v',str(count),'-c:v','libx264','-preset','slow','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(dest)],check=True)
        probe=json.loads(subprocess.check_output([ffprobe,'-v','error','-show_entries','stream=codec_name,width,height,nb_frames,r_frame_rate:format=duration','-of','json',str(dest)]))
        report.append({'file':dest.relative_to(PROJECT).as_posix(),'measured':probe})
    # Contact sheets retain explicit engine/source labels; full-resolution originals remain.
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20) if Path('C:/Windows/Fonts/arial.ttf').exists() else ImageFont.load_default()
    sets={
        'qa_unity_views':['unity_crates_view_0_720','unity_crates_view_1_720','unity_crates_view_2_720','unity_crates_view_3_720','unity_gates_front_1080','unity_gates_top_1080','unity_colors_1080','unity_joins_1080'],
        'qa_occupancy':['unity_occupancy_00','unity_occupancy_40','unity_occupancy_71'],
    }
    for name,files in sets.items():
        width=640; height=392; sheet=Image.new('RGB',(width*2,height*((len(files)+1)//2)),(24,32,42)); draw=ImageDraw.Draw(sheet)
        for i,file in enumerate(files):
            image=Image.open(BASE/'previews'/(file+'.png')).convert('RGB'); image.thumbnail((width,360))
            x=i%2*width;y=i//2*height; sheet.paste(image,(x,y+32));draw.text((x+12,y+7),file,fill=(224,235,241),font=font)
        sheet.save(BASE/'previews'/(name+'.jpg'),quality=93)
    (BASE/'validation/video_validation.json').write_text(json.dumps({'passed':all(int(x['measured']['streams'][0]['nb_frames']) in (34,72) for x in report),'videos':report},indent=2)+'\n')
    print(json.dumps({'videos':[x['file'] for x in report]}))

if __name__=='__main__': main()
