"""Encode the actual Blender frames and a compact visual review sheet (Python + Pillow + ffmpeg)."""
from pathlib import Path
import subprocess
from PIL import Image, ImageDraw, ImageFont

BASE=Path(__file__).resolve().parents[1]
OUT=BASE/'previews'
for clip,count,loops in [('idle',60,3),('move',30,3),('push',19,1)]:
    filters=f'trim=end_frame={count},loop=loop={loops-1}:size={count}:start=0,setpts=N/(30*TB)'
    if clip=='push': filters='tpad=stop_mode=clone:stop_duration=0.6'
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-framerate','30',
                    '-i',str(OUT/'frames'/clip/'%04d.png'),'-vf',filters,
                    '-c:v','libx264','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/(clip+'.mp4'))],check=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
sheet=Image.new('RGB',(1280,1056),'#17232f'); draw=ImageDraw.Draw(sheet)
for row,(clip,frames,start) in enumerate([('idle',[0,15,45,60],0),('move',[0,8,15,23],70),('push',[0,3,15,18],110)]):
    for col,frame in enumerate(frames):
        image=Image.open(OUT/'frames'/clip/f'{frame:04d}.png').convert('RGB').resize((312,312))
        x=col*320+4; y=row*352+4
        sheet.paste(image,(x,y))
        draw.text((x+9,y+320),f'{clip.upper()}  |  Frame {start+frame}',font=font,fill='#dce6ef')
sheet.save(OUT/'animation_contact_sheet.png')
print('Packaged Idle (3 cycles), Move (3 cycles), Push and contact sheet.')
