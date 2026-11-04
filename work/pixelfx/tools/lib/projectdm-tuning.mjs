import fs from 'node:fs';
import {loadEffect,renderFrames} from './fxlib.mjs';

export function loadSettings(file){
 const value=JSON.parse(fs.readFileSync(file,'utf8'));
 if(!Array.isArray(value.profiles))throw Error('profiles array is required');
 const ids=new Set();
 for(const p of value.profiles){
  if(!/^[a-z]+$/.test(p.id)||ids.has(p.id))throw Error('Invalid/duplicate profile id');ids.add(p.id);
  for(const [key,min,max]of [['light',0,2],['density',.25,2],['width',.5,1.5],['spread',.5,1.4],['speed',.5,2],['size',.5,1.3],['tail',0,1.8],['starRadius',.6,1.4]])
   if(!Number.isFinite(p[key])||p[key]<min||p[key]>max)throw Error(`${p.id}: ${key} must be ${min}..${max}`);
  if(!Number.isInteger(p.seed)||p.seed<0||p.seed>1000000)throw Error('seed must be 0..1000000');
  if(!Array.isArray(p.palette)||p.palette.length<3||p.palette.some(x=>!/^#[a-f0-9]{6}$/i.test(x)))throw Error('Invalid palette');
 }
 return value;
}

export async function tunedRender(id,settings){
 const profile=settings.profiles.find(p=>p.id===id);if(!profile)throw Error('Missing profile: '+id);
 const fx=await loadEffect('projectdm/'+id);
 const meta={...fx.meta,duration:.5/profile.speed,options:{...fx.meta.options,...profile}};
 fx.meta=meta;fx.mod={...fx.mod,meta};
 const r=renderFrames(fx,{actor:false,bg:'none'});let clipped=0;
 // Render beyond the canvas so edits cannot silently crop a widened flame or enlarged particle.
 const margin=24,padded=renderFrames(fx,{actor:false,bg:'none',margin}),[ox,oy]=fx.meta.origin;
 for(const frame of padded.frames)for(let y=0;y<padded.h;y++)for(let x=0;x<padded.w;x++){
  if((frame.raw[y*padded.w+x]>>>24)<=24)continue;
  const dx=ox+(x-margin-ox)*profile.size,dy=oy+(y-margin-oy)*profile.size;
  if(dx<0||dy<0||dx>=r.w||dy>=r.h)clipped++;
 }
 if(profile.size!==1){
  const [ox,oy]=fx.meta.origin;
  for(const frame of r.frames){const input=frame.clear,output=new Uint8ClampedArray(input.length);
   for(let y=0;y<r.h;y++)for(let x=0;x<r.w;x++){
    const sx=Math.floor(ox+(x-ox)/profile.size),sy=Math.floor(oy+(y-oy)/profile.size);
    if(sx>=0&&sx<r.w&&sy>=0&&sy<r.h)output.set(input.subarray((sy*r.w+sx)*4,(sy*r.w+sx)*4+4),(y*r.w+x)*4);
   }
   frame.clear=output;frame.color=output;
  }
 }
 return {fx,r,clipped};
}
