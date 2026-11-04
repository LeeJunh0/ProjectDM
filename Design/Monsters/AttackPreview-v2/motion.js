// Original sprites stay unchanged: only the draw transform and separate attack effects animate.
function motion(row,t){
 const prep=Math.max(0,Math.min(1,(t-.65)/.6));
 if(t<.65)return {sx:1,sy:1,x:0,y:0};
 if(t<1.25){const ease=prep*prep*(3-2*prep),shake=prep>.65?Math.sin(t*65)*(row?.65:.4)*prep:0;
  return row?{sx:1+.05*ease,sy:1-.07*ease,x:shake,y:0}:{sx:1+.13*ease,sy:1+.1*ease,x:shake,y:0};
 }
 const age=t-1.25,k=Math.exp(-age*11),shake=Math.sin(age*45)*k;
 return row?{sx:1+.065*k,sy:1-.04*k,x:shake*1.7,y:0}:{sx:1+.08*k,sy:1-.1*k,x:-2*k,y:-Math.sin(Math.min(1,age/.22)*Math.PI)*1.5*k};
}
if(typeof module!=='undefined')module.exports={motion};
