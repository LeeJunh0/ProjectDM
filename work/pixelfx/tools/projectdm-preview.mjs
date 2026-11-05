import fs from 'node:fs';import path from 'node:path';import {spawnSync} from 'node:child_process';
import {loadEffect,renderFrames} from './lib/fxlib.mjs';
import {loadSettings,tunedRender} from './lib/projectdm-tuning.mjs';
const root=path.resolve('../..'),dir=path.join(root,'Design/Skills/PixelFX-v1');
const manifest=JSON.parse(fs.readFileSync(path.join(dir,'skill-effects.json'),'utf8'));
const png=p=>'data:image/png;base64,'+fs.readFileSync(p).toString('base64');
const names={missile:'마력탄',beam:'마력 광선',satellite:'마력 위성',cone:'화염 부채꼴',firefield:'화염 지대',meteor:'화염 유성',chain:'연쇄 번개',orb:'번개 구체',thunder:'낙뢰',frostshard:'빙결 공전 조각',blizzard:'빙결 폭풍',shatter:'빙결 파쇄',hit:'명중 파편',fireimpact:'유성 충돌',mote:'획득·구매 반짝임'};
const order=['missile','beam','satellite','cone','firefield','meteor','chain','orb','thunder','frostshard','blizzard','shatter','hit','fireimpact','mote'];
const images=[png(path.join(dir,'skill-effects.png')),...['meadow','dungeon'].map(n=>png(path.join(root,`Docs/QA/2026-11-03-${n}-sharp.png`)))];
const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Project DM · 픽셀 스킬 연출</title><style>body{margin:0;background:#181a24;color:#ece5d9;font:15px/1.6 system-ui}main{max-width:1280px;padding:24px;margin:auto}h1{font-size:25px;margin:0}p{color:#b6b2b1}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}article{background:#242837;border:1px solid #424658;padding:12px}h2{font-size:16px;margin:0}canvas{width:100%;image-rendering:pixelated}button,select{font:inherit;background:#353b50;color:#eee2c7;border:1px solid #747083;padding:7px 14px}.controls{display:flex;gap:12px;margin:20px 0;align-items:center;flex-wrap:wrap}.note{font-size:13px}@media(max-width:720px){.grid{grid-template-columns:1fr 1fr}}@media(max-width:450px){.grid{grid-template-columns:1fr}}</style><main><h1>기본·고유 스킬 픽셀 연출</h1><p>빛의 중심과 끝, 흩어지는 입자를 정리한 버전이에요. 바닥을 바꿔 색과 윤곽을 비교할 수 있어요.</p><div class="controls"><select id="biome"><option value="1">초원</option><option value="2">석재 던전</option></select><button id="play">일시정지</button><select id="speed"><option value="1">1배속</option><option value=".5">0.5배속</option></select><span id="status">준비 중…</span></div><div class="grid">${order.map(id=>`<article><h2>${names[id]}</h2><canvas id="${id}" width="240" height="144"></canvas></article>`).join('')}</div><p class="note">시안은 움직임 비교를 위해 반복 재생해요. 공격 범위·피해·마나 소모는 기존 게임 수치를 사용해요.</p></main><script>
const manifest=${JSON.stringify(manifest)},sources=${JSON.stringify(images)},order=${JSON.stringify(order)},images=sources.map(()=>new Image());let loaded=0,playing=true,time=0,last=null;
function draw(){let bg=images[Number(document.querySelector('#biome').value)];for(const id of order){let c=document.getElementById(id).getContext('2d');c.imageSmoothingEnabled=false;c.drawImage(bg,200,160,480,288,0,0,240,144);let clip=manifest.clips.find(x=>x.id===id),t=clip.loop?time%(clip.frames.length/clip.fps):time%(clip.frames.length/clip.fps+.55);let frame=clip.frames[Math.min(clip.frames.length-1,Math.floor(t*clip.fps))];c.drawImage(images[0],frame.x,frame.y,frame.width,frame.height,120-frame.width*.9,72-frame.height*.9,frame.width*1.8,frame.height*1.8);}}
function tick(now){if(last!==null&&playing)time+=Math.min(.1,(now-last)/1000)*Number(document.querySelector('#speed').value);last=now;draw();requestAnimationFrame(tick)}
images.forEach((im,i)=>{im.onload=()=>{if(++loaded===images.length){document.querySelector('#status').textContent='재생 중';requestAnimationFrame(tick)}};im.onerror=()=>{document.querySelector('#status').textContent='이미지 로딩 실패'};im.src=sources[i]});document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시정지':'재생';document.querySelector('#status').textContent=playing?'재생 중':'일시정지'};
</script></html>`;fs.writeFileSync(path.join(dir,'index.html'),html);
// A script-independent motion preview of the two most changed directional effects.
for(const id of ['beam','cone','missile']){
 const {fx,r}=await tunedRender(id,loadSettings(process.env.PROJECTDM_PIXELFX_TUNING??path.join(dir,'tuning.json'))),w=240,h=144;
 const bgfile=path.join(root,'Docs/QA/2026-11-03-dungeon-sharp.png');
 const d=spawnSync('ffmpeg',['-v','error','-i',bgfile,'-vf','crop=480:288:200:160,scale=240:144:flags=neighbor','-f','rawvideo','-pix_fmt','rgba','-'],{maxBuffer:4e6});if(d.status!==0)throw Error(d.stderr.toString());
 const raw=Buffer.alloc(w*h*3*r.n),scale=2;
 for(let i=0;i<r.n;i++){const frame=r.frames[i].clear,left=(w-r.w*scale)/2,top=(h-r.h*scale)/2;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){const sx=Math.floor((x-left)/scale),sy=Math.floor((y-top)/scale),dst=(i*w*h+y*w+x)*3,bg=(y*w+x)*4;
   for(let k=0;k<3;k++){let v=d.stdout[bg+k];if(sx>=0&&sx<r.w&&sy>=0&&sy<r.h){let at=(sy*r.w+sx)*4,a=frame[at+3]/255;v=v*(1-a)+frame[at+k]*a}raw[dst+k]=v;}
  }
 }
 const result=spawnSync('ffmpeg',['-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s',w+'x'+h,'-r','24','-i','-','-filter_complex','split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none','-loop','0',path.join(dir,id+'-game.gif')],{input:raw,maxBuffer:4e6});if(result.status!==0)throw Error(result.stderr.toString());
}
console.log('Self-contained 15-effect game-background preview and beam/cone/missile GIFs exported.');
