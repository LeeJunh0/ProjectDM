// Flame puffs adapted from Sprite Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire),
// MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit, Rng, TAU, sparkle, crystal, hex, cycle } from '../../core/index.js';

import {firePuff,hotFirst} from '../fire/_lib.js';

export const palettes={arcane:['#33213f','#613676','#a052bb','#d488e6','#f1c9fa'],fire:['#472334','#8e3431','#d25c28','#ef9e43','#ffe1a0'],ice:['#233b59','#3c6d90','#6fa6c4','#a5d3df','#e1f4ef'],electric:['#282b52','#40568c','#687fbd','#a0cced','#e2f4f7']};
export function drawEffect(p,t,o){
 t*=o.speed??1;
 const C=fit(o.palette,6),R=new Rng(o.seed),q=t/.5,ph=q*TAU,quality=o.revision??3;
 const light=quality===1?.45:quality===2?.22:.13;
 const id=o.motif,one=['shatter','hit','fireimpact'].includes(id),fade=one?Math.pow(Math.max(0,1-q),.7):1;
 if(one&&t>=.5)return;
 const glow=(x,y,r,k=1)=>p.glow(x,y,r,C[3],light*k*fade);
 const mote=(i,x,y,a=1)=>{let s=i%4===0?2:1;p.rect(x,y,s,s,C[i%3+2],a*fade);if(i%5===0&&id!=='cone'&&id!=='firefield'&&id!=='meteor')sparkle(p,x,y,2,C[5],C[3],a*fade);};
 if(id==='missile'){
  p.poly([[27,-2],[34,-5],[42,0],[34,5],[27,2],[30,0]],C[1]);
  p.poly([[30,-2],[35,-3],[40,0],[34,3],[30,1]],C[3]);
  p.poly([[33,-1],[37,-1],[40,0],[34,1]],C[5]);p.line(31,-2,35,-3,C[4]);
  const tail=o.tail??1;
  if(tail>0){p.line(28-7*tail,-1,27,-1,C[2],.8);p.line(29-4*tail,2,28,2,C[3],.65);}
  glow(35,0,8,.7);
  for(let i=0;i<Math.round(3*(o.density??1));i++){let x=23-((q+i*.31)%1)*6,y=(i-1)*3;p.dot(x,y,C[2+i%2],.6);}
 }else if(id==='beam'){
  const points=[];for(let i=0;i<=40;i++){let x=-40+i*2;points.push([x,Math.sin(x*.15-ph*2)*.6]);}
  for(let i=1;i<points.length;i++)p.beam(...points[i-1],...points[i],5*(o.width??1),C[1]);
  for(let i=1;i<points.length;i++)p.beam(...points[i-1],...points[i],3*(o.width??1),C[3]);
  p.polyline(points,C[4]);
  for(let i=0;i<points.length;i++){let [x,y]=points[i],pulse=Math.pow(Math.max(0,Math.cos((x+40)/80*TAU*2-ph*3)),8);
   if(pulse>.4)p.line(x-1,y-.4,x+1,y-.4,C[5]);
   for(let strand=0;strand<2;strand++){let wave=Math.sin(x*.22-ph*2+strand*Math.PI),off=wave*(3+Math.sin(x*.08)*.6);if(Math.cos(x*.22-ph*2+strand*Math.PI)>-.2)p.dot(x,y+off,C[strand?3:4],.85)}
  }
  for(let i=0;i<Math.round(5*(o.density??1));i++){let x=-36+((q*2+i*.219)%1)*72,y=Math.sin(i*2.4+ph)*6;mote(i,x,y,.6)}glow(-30,0,9,.5);glow(28,0,9,.5);
 }else if(id==='chain'){
  let prev=[-40,0];for(let i=1;i<=18;i++){let x=-40+i*80/18,y=R.range(-4,4)*(.65+.35*Math.sin(ph+i));
   p.line(prev[0],prev[1],x,y,C[4]);if(i%4===0)p.dot(x,y-1,C[5]);
   if(i%5===0){let a=R.range(-1,1)*8;p.line(x,y,x-5,y+a,C[2],.7);p.line(x-5,y+a,x-7,y+a*1.1,C[1],.5)}prev=[x,y];}
  glow(-25,0,9);glow(22,0,9);for(let i=0;i<Math.round(4*(o.density??1));i++)mote(i,R.range(-35,35),R.range(-8,8),.55);
 }else if(id==='cone'){
  const H=hotFirst(fit(o.palette,9)),cloud=[];
  cycle(t,.5,Math.round(40*(o.density??1)),o.seed+201,r=>r.range(.23,.36),(age,life,r)=>{
   let u=age/life,x=4+u*35,e=r.range(-1,1),y=e*(2+x*.39)*(o.spread??1)-u*u*1.5;
   cloud.push({u,x,y,s:r.range(3,5.2)*(o.width??1)*(1+u*.65),f:r.next()});
  });
  cloud.sort((a,b)=>b.u-a.u);
  for(const f of cloud)firePuff(p,f.x,f.y,1-f.u*.92,f.s,f.f,H,.025,.65,-.55);
  p.poly([[1,-2],[8,-3],[12,0],[7,3],[2,2]],C[3]);p.line(3,-1,10,0,C[5]);glow(9,0,10,.7);
  cycle(t,.5,7,o.seed+501,.19,(age,life,r)=>{let u=age/life,x=27+u*15,y=r.range(-.4,.4)*x;p.dot(x,y,C[u<.5?4:2],1-u);});
 }else if(id==='satellite'||id==='orb'){
  p.circle(0,0,id==='orb'?8:5,C[0],.75);p.arc(0,0,id==='orb'?9:6,1,ph,ph+4.4,C[3]);p.arc(0,0,id==='orb'?6:4,1,-ph,-ph+3.2,C[4]);
  sparkle(p,-2,-2,3,C[5],C[3]);glow(-1,-1,13);
  for(let i=0;i<Math.round(5*(o.density??1));i++){let a=i*2.399+ph,r=11+i%3*2;mote(i,Math.cos(a)*r,Math.sin(a)*r*.65,.7)}
  if(id==='orb')for(let i=0;i<3;i++){let a=i*2.1+ph;const x=Math.cos(a)*9,y=Math.sin(a)*9;p.line(x,y,x+R.range(-4,4),y+R.range(-4,4),i===1?hex('#ffc588'):C[4],.8)}
 }else if(id==='firefield'){
  for(let i=0;i<Math.round(9*(o.density??1));i++){if(i===2||i===7)continue;let a=i*TAU/9+Math.sin(i*3.1)*.16,r=22+R.range(-3,3),x=Math.cos(a)*r,y=Math.sin(a)*r*.65,phase=(q+i*.37)%1,h=4+8*(1-phase);
   p.poly([[x-3,y],[x-2,y-h*.6],[x+1,y-h],[x+2,y-h*.3],[x+3,y]],C[2],.8);p.line(x,y-1,x+Math.sin(ph+i)*1.5,y-h*.7,C[4],.8);if(i%3===0)glow(x,y-2,7,.7)}
  p.arc(0,0,23,1,ph,ph+1.6,C[1],.6);for(let i=0;i<Math.round(6*(o.density??1));i++)mote(i,R.range(-24,24),R.range(-20,12)-q*5,.6);
 }else if(id==='meteor'){
  for(let i=0;i<16;i++){let x=-i*.65,y=-i*1.5,w=(1-i/18)*(6+Math.sin(i*.8-ph*2)*.6);p.circle(x,y,w,C[i>9?1:2],1-i/19);if(i<12)p.line(x-1,y,x,y-4,C[3],.8)}
  p.poly([[-7,-3],[-3,-8],[4,-6],[8,0],[4,6],[-4,5]],C[0]);p.line(-5,-4,-1,-6,C[4]);p.line(-1,-6,5,-3,C[3]);p.line(4,1,2,4,C[2]);glow(-2,-4,13,.8);
  for(let i=0;i<Math.round(4*(o.density??1));i++)mote(i,-R.range(3,16),-R.range(10,31)+Math.sin(ph+i)*2,.65);
 }else if(id==='thunder'){
  let prev=[0,-37];for(let i=1;i<=12;i++){let y=-37+i*6,x=i===12?0:R.range(-4,4);p.beam(prev[0],prev[1],x,y,2,C[2]);p.line(prev[0],prev[1],x,y,C[5]);if(i%3===0)p.line(x,y,x+R.range(-9,9),y-6,C[3],.65);prev=[x,y]}
  glow(0,26,13);sparkle(p,0,30,5,C[5],C[3]);
 }else if(id==='frostshard'){
  crystal(p,0,8,20,8,2,C,{a:1,white:false});p.dot(1,-7+Math.sin(ph)*5,C[4]);p.line(-1,-10,-1,3,C[5],.85);glow(0,-2,10,.6);for(let i=0;i<Math.round(3*(o.density??1));i++)mote(i,R.range(-8,8),8+R.range(0,10),.5);
 }else if(id==='blizzard'){
  for(let i=0;i<Math.round(15*(o.density??1));i++){let a=i*2.399+ph,r=9+i%6*4,x=Math.cos(a)*r,y=Math.sin(a)*r*.8;p.line(x,y,x+5,y-3,C[i%3+2],.5);if(i%3===0){let starR=Math.max(17,r+3)*(o.starRadius??1);sparkle(p,Math.cos(a)*starR,Math.sin(a)*starR*.8,2,C[4],C[2],.8)}}
  for(let i=0;i<3;i++)p.arc(0,0,15+i*6,1,-ph+i*2,-ph+i*2+.8,C[2],.35);
 }else if(id==='shatter'||id==='hit'||id==='fireimpact'){
  let r=(id==='hit'?10:30)*(1-Math.exp(-q*6)),n=Math.round((id==='hit'?7:id==='fireimpact'?15:11)*(o.density??1));
  for(let i=0;i<n;i++){let a=i*2.399+R.range(-.13,.13),rr=r*(.55+R.range(0,.45)),x=Math.cos(a)*rr,y=Math.sin(a)*rr;
   if(id!=='hit')p.poly([[x,y-(4+i%3*2)*fade],[x+2*fade,y],[x,y+3*fade],[x-2*fade,y]],C[i%3+2],fade);
   else p.line(x*.5,y*.5,x,y,C[i%3+2],fade);}
  if(q<.25)sparkle(p,0,0,(id==='hit'?4:7)*(1-q/.25),C[5],C[3],fade);glow(0,0,12,fade);
 }else if(id==='mote'){
  sparkle(p,0,0,3+Math.sin(ph)*.6,C[5],C[3]);p.dot(-3,2,C[2],.7);glow(0,0,7,.4);
 }
}
