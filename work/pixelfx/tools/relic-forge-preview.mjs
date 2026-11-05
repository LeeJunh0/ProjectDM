// Self-contained animation comparison; never loads or changes game saves.
import fs from 'node:fs';import path from 'node:path';
const root=path.resolve('../..'),dir=path.join(root,'Design/Relics/AlchemyWorkshop-v1');
function readClip(id){
 const folder=path.join(dir,'final/relic-forge',id),data=JSON.parse(fs.readFileSync(path.join(folder,id+'.json')));
 return {id,src:'data:image/png;base64,'+fs.readFileSync(path.join(folder,id+'.png')).toString('base64'),frames:data.frames.map(f=>f.frame)};
}
const clips=['success','failure'].map(readClip),idle=readClip('idle');
const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>귀환 작업실 · 낡은 연금 가마</title>
<style>*{box-sizing:border-box}body{margin:0;background:#100e19;color:#eee3c7;font:16px/1.6 system-ui,sans-serif}main{max-width:1120px;margin:auto;padding:28px 24px}h1{margin:0;font-size:26px}header p{color:#aaa1ac;margin:4px 0 20px}nav{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:22px}button{cursor:pointer;background:#272031;border:1px solid #b88b3a;color:#f4e4be;padding:10px 18px;font:inherit;border-radius:0}button:hover{background:#3b2c40}button:disabled{opacity:.4;cursor:wait}select{background:#211b2a;color:inherit;border:1px solid #6c5840;padding:8px}section{display:grid;grid-template-columns:1fr 1fr;gap:24px}.card{background:linear-gradient(130deg,#211927,#13111d 56%,#1b1521);border:2px solid #97733b;box-shadow:inset 0 0 0 3px #302332,0 0 0 2px #332332;padding:22px;text-align:center;position:relative}.card h2{margin:0;font-size:25px}.hint{font-size:14px;color:#aa9eac}.stage{display:flex;justify-content:center;overflow:hidden;min-height:288px;background:radial-gradient(ellipse at center,#2b1d3a55,transparent 66%)}canvas{width:320px;height:288px;image-rendering:pixelated;max-width:100%;object-fit:contain}.phase{height:44px;font-size:23px;font-weight:700;color:#e4d3ed}.value{font-size:26px;color:#eed39b;min-height:46px}.sub{font-size:15px;color:#bcb0bd;min-height:54px}.card button{width:100%;margin-top:12px}.ledger{display:flex;justify-content:space-between;color:#bbb09d;border-top:1px solid #4c3b2f;margin-top:16px;padding-top:14px;font-size:14px}.track{height:4px;background:#342a3d;margin:14px 0}.track i{display:block;height:100%;background:#b58bd1;transform-origin:left;transform:scaleX(0)}footer{color:#a89daa;margin-top:22px;font-size:14px}input[type=range]{width:240px;accent-color:#b58bd1}.success.reveal{border-color:#d8b262;box-shadow:inset 0 0 0 3px #302332,0 0 18px #d5a65822}.failure.reveal{border-color:#655567}@media(max-width:760px){section{grid-template-columns:1fr}main{padding:20px 14px}h1{font-size:23px}}</style>
<main><header><h1>귀환 작업실 · 낡은 연금 가마</h1><p>원정 금화의 마력을 추출해 조각으로 굳히는 작업대예요. 비교 화면에서는 실제 재화를 쓰지 않아요.</p></header>
<nav><button id="replay" disabled>둘 다 다시 재생</button><button id="pause" disabled>일시정지</button><label><input id="loop" type="checkbox" checked> 반복 재생</label><label>배속 <select id="speed"><option value="0.75">0.75×</option><option value="1" selected>1×</option><option value="1.5">1.5×</option></select></label><input id="scrub" aria-label="프레임 확인" type="range" min="0" max="1.4" step="0.01" value="0"></nav>
<section>${clips.map((c,i)=>`<article class="card ${c.id}" id="card${i}"><h2>낡은 연금 가마</h2><div class="hint">매 도전 성공 확률 50% · ${i?'실패':'성공'} 연출 보기</div><div class="stage"><canvas width="320" height="288" id="canvas${i}"></canvas></div><div class="phase" id="phase${i}">리소스 준비 중</div><div class="value" id="value${i}">미확정 조각 0개</div><div class="sub" id="sub${i}">가마에 금화 넣기 → 응축 → 꺼내기 또는 재응축</div><div class="track"><i id="track${i}"></i></div><div class="ledger"><span id="gold${i}">보유 금화 1,000</span><span>확보한 조각 3개 유지</span></div><button id="again${i}" disabled>${i?'실패':'성공'} 연출 다시 보기</button></article>`).join('')}</section>
<footer id="status">이미지를 불러오는 중…</footer></main>
<script>
const clips=${JSON.stringify(clips)},idle=${JSON.stringify(idle)};
const states=clips.map(()=>({time:0}));let playing=true,last=null,ready=false;
const images=[...clips,idle].map(()=>new Image());
function reset(index){if(index===undefined)states.forEach(s=>s.time=0);else states[index].time=0;playing=true;document.querySelector('#pause').textContent='일시정지';}
function draw(index){
 const s=states[index],c=clips[index],t=Math.min(s.time,1.4),ctx=document.querySelector('#canvas'+index).getContext('2d');
 ctx.clearRect(0,0,320,288);ctx.imageSmoothingEnabled=false;
 const base=idle.frames[Math.floor(s.time*24)%idle.frames.length];ctx.drawImage(images[2],base.x,base.y,base.w,base.h,0,0,320,288);
 const frame=c.frames[Math.min(c.frames.length-1,Math.floor(t*24))];
 const shake=index===1&&t>=.72&&t<.86?Math.round(Math.sin(t*90)*2*(1-(t-.72)/.14)):0;
 if(index===0&&t>=1.25){const held=c.frames[26];ctx.drawImage(images[index],held.x+65,held.y+46,30,34,130,92,60,68);}
 else ctx.drawImage(images[index],frame.x,frame.y,frame.w,frame.h,shake,0,320,288);
 const revealed=t>=.72;
 document.querySelector('#card'+index).classList.toggle('reveal',revealed);
 document.querySelector('#phase'+index).textContent=t<.28?'금화를 가마에 넣어요':!revealed?'가마 속 마력을 응축해요':index===0?'조각이 굳었어요':'응축에 실패했어요';
 document.querySelector('#value'+index).textContent='미확정 조각 '+(revealed&&index===0?'1':'0')+'개';
 document.querySelector('#sub'+index).textContent=!revealed?'잠깐 응축된 뒤 결과가 드러나요.':index===0?'조각을 꺼내거나, 금화 추가 없이 더 응축할 수 있어요.':'가마 속 조각이 부서져 연기로 흩어져요. 보관한 조각은 유지돼요.';
 document.querySelector('#gold'+index).textContent='보유 금화 '+(t>0?'900':'1,000');
 document.querySelector('#track'+index).style.transform='scaleX('+Math.min(1,t/1.4)+')';
}
function tick(now){
 const dt=last===null?0:Math.min(.06,(now-last)/1000);last=now;
 if(ready){for(let i=0;i<states.length;i++){if(playing){states[i].time+=dt*Number(document.querySelector('#speed').value);if(states[i].time>2.7&&document.querySelector('#loop').checked)states[i].time=0;}draw(i);}document.querySelector('#scrub').value=Math.min(1.4,states[0].time);}
 requestAnimationFrame(tick);
}
document.querySelector('#replay').onclick=()=>reset();
document.querySelector('#pause').onclick=()=>{playing=!playing;document.querySelector('#pause').textContent=playing?'일시정지':'재생';};
document.querySelector('#scrub').oninput=e=>{playing=false;states.forEach(s=>s.time=Number(e.target.value));document.querySelector('#pause').textContent='재생';};
states.forEach((s,i)=>document.querySelector('#again'+i).onclick=()=>reset(i));
Promise.all(images.map((img,i)=>new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=()=>reject(Error('이미지를 읽지 못했어요.'));img.src=(i<clips.length?clips[i]:idle).src;}))).then(()=>{ready=true;document.querySelectorAll('button').forEach(b=>b.disabled=false);document.querySelector('#status').textContent='금화 넣기 → 가마에서 응축 → 조각이 굳거나 연기로 소멸. 같은 리소스와 동작을 게임에도 연결했어요.';}).catch(e=>document.querySelector('#status').textContent=e.message);
requestAnimationFrame(tick);
</script></html>`;
fs.writeFileSync(path.join(dir,'index.html'),html);console.log('Forge success/failure comparison exported.');
