import {fit,hex,Rng,TAU,sparkle} from '../../core/index.js';

const metal=['#1c1d29','#343440','#555260','#897a7b'].map(hex);
const wood=['#241a20','#4c3430','#78543b'].map(hex);
const gold=['#5b3116','#c48229','#f2bd58','#fff0b2'].map(hex);
export const coinCount=amount=>amount<=0?0:Math.min(Math.floor(amount),Math.max(1,Math.min(128,Math.ceil(8*Math.log1p(amount)))));
export function coinPhase(age,index,count){
 if(count<=0)return 2;
 const life=age-(count<=1?0:.22*Math.pow(index/(count-1),.65)+.025*coinVariation(index,97));
 return life<0?-1:life<.18?life/.18:1+(life-.18)/.20;
}
function coinVariation(index,salt){let v=(Math.imul(index+1,747796405)+salt)>>>0;v^=v>>>16;v=Math.imul(v,2246822519)>>>0;v^=v>>>13;return(v&0xffffff)/16777215;}
export function drawDeposit(p,t,amount){
 const count=coinCount(amount);
 for(let i=0;i<count;i++){
  const q=coinPhase(t,i,count);if(q<0||q>=2)continue;
  const V=salt=>coinVariation(i,salt),sx=-22+10*V(11),sy=-6-5*V(23),ex=-8+16*V(37),ey=7+2*V(53),m=Math.max(0,q-1);
  const x=q<1?sx+(ex-sx)*q:ex,y=q<1?sy+(ey-sy)*q*q:ey+m*2-Math.sin(m*Math.PI)*1.2;
  const w=q<1?3*(.45+.55*Math.abs(Math.cos(q*12+i))):3*(1-m*.85),h=q<1?3:3*(1-m*.97);
  p.ellipse(x,y,w/2,h/2,gold[0],1-m);p.ellipse(x,y,w*.35,h*.35,gold[2],1-m);
  p.line(x,y-h*.22,x,y+h*.22,gold[3],1-m);
  if(m>0){const r=1+5*m,a=(1-m)*.65;
   p.rect(ex-r-1,ey-.5,2,1,gold[2],a);p.rect(ex+r-1,ey-.5,2,1,gold[2],a);
   p.rect(ex-r*.6,ey-1.5,r*1.2,1,gold[3],a);
   p.dot(ex+(V(71)-.5)*m*8,ey-m*5,gold[3],a);
  }
 }
}
function kettle(p,t,C,charge,a,deposit){
 p.ellipse(0,57,59,3,wood[0],.5*a);
 p.poly([[-58,44],[-48,39],[49,39],[59,44],[56,48],[-57,48]],wood[1],a);
 p.rect(-57,47,114,8,wood[0],a);p.line(-53,44,48,44,wood[2],.7*a);
 p.line(-42,50,-16,50,wood[1],a);p.line(3,51,44,51,wood[1],a);
 p.rect(-16,34,6,9,metal[0],a);p.rect(11,34,6,9,metal[0],a);
 p.arc(-23,16,6,2,1.6,4.8,metal[2],a);p.arc(23,16,6,2,-1.5,1.6,metal[1],a);
 p.poly([[-19,8],[-25,14],[-27,23],[-22,32],[-11,39],[12,39],[23,32],[27,22],[25,13],[19,8]],metal[0],a);
 p.poly([[-16,12],[-22,17],[-23,24],[-18,31],[-7,35],[11,35],[20,29],[23,20],[19,12]],metal[1],a);
 p.line(-22,18,-21,25,metal[2],a);p.line(-18,28,-11,32,metal[2],.8*a);
 p.line(10,33,16,29,metal[0],a);p.rect(-12,34,17,2,metal[0],a);
 p.dot(-17,17,gold[1],a);p.dot(15,20,gold[0],a);p.dot(-12,29,metal[3],.6*a);
 p.ellipse(0,8,21,6,metal[2],a);p.ellipse(0,8,17,4,gold[1],a);
 p.ellipse(-4,7,8,2,gold[2],.6*a);
 const ph=t*TAU;
 p.line(-8+Math.sin(ph)*3,8,-2+Math.sin(ph)*3,8,gold[3],.5*a);
 p.dot(8,7+Math.sin(ph*2),gold[3],(.3+charge*.5)*a);
 p.arc(0,8,20,2,0,Math.PI,metal[3],a);p.line(-14,3,-7,2,metal[3],a);
 if(charge>.2)p.glow(0,7,17,gold[2],charge*.14*a);
}

export function drawForge(p,t,o){
 const C=fit(o.palette,6);
 if(o.outcome==='idle'){kettle(p,t,C,0,1,false);return;}
 if(t>=1.4)return;
 const R=new Rng(o.seed),end=Math.min(1,(1.4-t)/.2),hit=.72;
 const charge=Math.min(1,t/hit),angle=t*8+charge*charge*8,deposit=o.deposit!==false;
 kettle(p,t,C,charge,end,deposit);
 if(t<hit){
  if(deposit)drawDeposit(p,t,o.amount??100);
  for(let i=0;i<8;i++){
   const ang=i*2.399+angle,r=27-charge*15+R.range(-3,3),x=Math.cos(ang)*r,y=6+Math.sin(ang)*r*.4;
   p.line(x,y,x-Math.cos(ang)*3,y-Math.sin(ang)*2,gold[i%3+1],.25+charge*.5);
   if(i%4===0)p.dot(x,y,gold[2],.7);
  }
  if(charge>.45){const h=(charge-.45)*13;p.poly([[-3,8],[-2,8-h],[2,6-h],[4,8]],gold[2],charge);}
  for(let i=0;i<3;i++){
   const u=(t*1.4+i*.33)%1,x=-11+i*10+Math.sin(u*4+i)*2,y=1-u*18;
   p.disc(x,y,1+u*2.5,gold[1],u*(1-u)*.45);
  }
  p.arc(0,5,17-charge*5,1,angle,angle+1.2,gold[1],.55);
  return;
 }
 const age=t-hit;
 if(o.outcome==='success'){
  const flash=Math.max(0,1-age/.09);
  if(flash>0)sparkle(p,0,5,10*flash,C[5],gold[3],flash);
  const pop=Math.min(1,age/.17),y=4-13*Math.sin(pop*Math.PI*.5)+Math.sin(age*10)*.6,s=.3+.7*pop;
  p.poly([[-8,-3],[-3,-12],[4,-10],[9,-2],[4,10],[-4,9]].map(([x,py])=>[x*s,y+py*s]),C[1],end);
  p.poly([[-6,-3],[-2,-10],[2,-2],[-3,7]].map(([x,py])=>[x*s,y+py*s]),C[3],end);
  p.poly([[-2,-10],[3,-8],[7,-2],[2,6]].map(([x,py])=>[x*s,y+py*s]),C[2],end);
  p.line(-2*s,y-8*s,-s,y+4*s,C[5],.85*end);p.glow(0,y,17,C[3],.18*end);
  for(let i=0;i<16;i++){
   const life=R.range(.35,.64),u=(age-i*.009)/life,ang=R.range(0,TAU),speed=R.range(18,39);
   if(u<0||u>1)continue;
   const x=Math.cos(ang)*(8+u*speed),py=3+Math.sin(ang)*(8+u*speed)*.6-u*12;
   if(i%5===0)sparkle(p,x,py,2*(1-u)+.5,gold[3],C[3],(1-u)*end);
   else p.rect(x,py,R.range(1,2.5),1+R.next(),i%3===0?gold[2]:C[3],(1-u)*end);
  }
 }else{
  const u=Math.min(1,age/.65);
  for(let i=0;i<7;i++){
   const x=R.range(-9,9)+R.range(-7,7)*u,y=5-u*R.range(19,36),size=R.range(2,4)*(1+u*.4);
   p.disc(x,y,size,hex(i%2?'#716775':'#5b486b'),Math.sin(Math.PI*u)*(1-u)*.65*end);
   if(u<.35){const py=6-8*u+12*u*u;p.poly([[x-2,py-2],[x+2,py],[x,py+2]],C[2],(1-u*2)*end);}
  }
  if(age<.14){p.line(-4,1,2,6,C[3],1-age/.14);p.line(2,6,-2,10,C[2],1-age/.14);}
  p.glow(0,6,12*(1-u),C[2],.1*(1-u));
 }
}
