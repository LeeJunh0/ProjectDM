const fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process');
const {motion}=require('./motion.js');
const root=path.resolve(__dirname,'../../..');
const sources=['Assets/GameContent/Art/Pixel32/Monsters/Meadow/3/Walk_Meadow_3_0.png','Assets/GameContent/Art/Pixel32/Monsters/Dungeon/4/Walk_Dungeon_4_0.png'];
const data=sources.map(p=>fs.readFileSync(path.join(root,p)));
const urls=data.map(b=>'data:image/png;base64,'+b.toString('base64'));
const effects=fs.readFileSync(path.join(__dirname,'motion.js'),'utf8');
const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>원본 몬스터 · 공격 연출</title>
<style>*{box-sizing:border-box}body{margin:0;background:#181a23;color:#e8e2d6;font:15px/1.6 system-ui}main{max-width:1100px;margin:auto;padding:28px}h1{font-size:24px;margin:0}p{color:#b9b4ae}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}article{padding:20px;background:#232735;border:1px solid #454959}h2{font-size:19px;margin:0}canvas{width:100%;image-rendering:pixelated}button{font:inherit;color:#eee;background:#373c50;border:1px solid #73778d;padding:8px 16px;cursor:pointer}.controls{display:flex;gap:18px;align-items:center;margin:18px 0}.phase{color:#e2c891}img.original{width:64px;height:64px;image-rendering:pixelated}.compare{display:flex;gap:12px;align-items:center;color:#aaa}a{color:#dfc58b}@media(max-width:700px){.grid{grid-template-columns:1fr}}</style>
<main><h1>원본 스프라이트 그대로 · 공격 연출</h1><p>새 그림 없이 크기 변화와 작은 움찔만 추가했어요. 게임에는 아직 적용하지 않았어요.</p><div class="controls"><button id="play">일시정지</button><span id="status">로딩 중…</span></div><section class="grid">
${['버섯 요정','망령 갑옷'].map((name,i)=>`<article><h2>${name}</h2><p>${i?'살짝 몸을 낮춤 → 짧게 움찔 → 복귀':'살짝 부풀기 → 포자를 뱉으며 움찔 → 복귀'}</p><canvas id="scene${i}" width="240" height="144"></canvas><div class="phase" id="phase${i}"></div><div class="compare"><img class="original" src="${urls[i]}" alt="게임 원본">게임 원본 · 같은 이미지를 사용</div><p><a href="${i?'armor':'mushroom'}-original.gif">몸동작 GIF 보기</a></p></article>`).join('')}</section><p>몸동작만 표시합니다. 인디케이터·투사체·타격 이펙트는 포함하지 않습니다.</p></main>
<script>${effects}
const urls=${JSON.stringify(urls)},sprites=urls.map(()=>new Image());let ready=0,playing=true,time=0,last=null;
function draw(row,t){const c=document.querySelector('#scene'+row).getContext('2d');c.imageSmoothingEnabled=false;c.fillStyle=row?'#30364b':'#2c3b30';c.fillRect(0,0,240,144);const m=motion(row,t),x=96+m.x*2,bottom=105+m.y*2,w=64*m.sx,h=64*m.sy;c.fillStyle='#10151c60';c.fillRect(76,104,40,4);c.drawImage(sprites[row],x-w/2,bottom-h,w,h);
document.querySelector('#phase'+row).textContent=t<.65?'대기':t<1.25?'준비 · '+(row?'몸을 낮추기':'살짝 부풀기'):t<1.48?'공격 · 움찔':'복귀';}
function tick(now){if(last!==null&&playing)time+=Math.min(.1,(now-last)/1000);last=now;draw(0,time%2.6);draw(1,time%2.6);requestAnimationFrame(tick)}
sprites.forEach((im,i)=>{im.onload=()=>{if(++ready===2){document.querySelector('#status').textContent='재생 중';requestAnimationFrame(tick)}};im.onerror=()=>{document.querySelector('#status').textContent='이미지 로딩 실패'};im.src=urls[i]});document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시정지':'재생';document.querySelector('#status').textContent=playing?'재생 중':'일시정지'};
</script></html>`;
fs.writeFileSync(path.join(__dirname,'index.html'),html);
// Assemble a standalone motion GIF from the same unmodified source pixels and motion curve.
for(let row=0;row<2;row++){
 const width=data[row].readUInt32BE(16),height=data[row].readUInt32BE(20);
 const decoded=cp.spawnSync('ffmpeg',['-v','error','-i',path.join(root,sources[row]),'-f','rawvideo','-pix_fmt','rgba','-'],{maxBuffer:4e6});if(decoded.status!==0)throw Error(decoded.stderr.toString());
 const sw=160,sh=120,count=63,frames=Buffer.alloc(sw*sh*3*count);
 for(let frame=0;frame<count;frame++){const m=motion(row,frame/24),w=64*m.sx,h=64*m.sy,left=80+m.x*2-w/2,top=91+m.y*2-h,offset=frame*sw*sh*3;
  for(let y=0;y<sh;y++)for(let x=0;x<sw;x++){let target=offset+(y*sw+x)*3,bg=row?[48,54,75]:[44,59,48];const sx=Math.floor((x-left)/w*width),sy=Math.floor((y-top)/h*height);
   for(let channel=0;channel<3;channel++){let value=bg[channel];if(sx>=0&&sx<width&&sy>=0&&sy<height){const source=(sy*width+sx)*4,alpha=decoded.stdout[source+3]/255;value=decoded.stdout[source+channel]*alpha+value*(1-alpha)}frames[target+channel]=Math.round(value)}
  }
 }
 const result=cp.spawnSync('ffmpeg',['-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s','160x120','-r','24','-i','-','-filter_complex','scale=320:240:flags=neighbor,split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none','-loop','0',path.join(__dirname,(row?'armor':'mushroom')+'-original.gif')],{input:frames,maxBuffer:4e6});if(result.status!==0)throw Error(result.stderr.toString());
}
console.log('Original-sprite HTML and motion GIFs built.');
