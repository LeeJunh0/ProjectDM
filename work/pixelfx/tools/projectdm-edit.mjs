// Unity Editor bridge. No shell, network or npm packages required.
import fs from 'node:fs';import path from 'node:path';
import {encodePNG} from './lib/png.mjs';
import {loadSettings,tunedRender} from './lib/projectdm-tuning.mjs';
const [mode,id,file,out]=process.argv.slice(2);
const settings=loadSettings(file);
if(mode==='preview'){
 const {fx,r,clipped}=await tunedRender(id,settings),cols=6,w=r.w*cols,h=r.h*Math.ceil(r.n/cols),pixels=new Uint8Array(w*h*4),frames=[];
 for(let i=0;i<r.n;i++){let x=i%cols*r.w,y=Math.floor(i/cols)*r.h;frames.push({x,y,width:r.w,height:r.h});for(let row=0;row<r.h;row++)pixels.set(r.frames[i].clear.subarray(row*r.w*4,(row+1)*r.w*4),((y+row)*w+x)*4);}
 fs.mkdirSync(out,{recursive:true});
 fs.writeFileSync(path.join(out,'preview.png'),encodePNG(w,h,pixels));
 fs.writeFileSync(path.join(out,'preview.json'),JSON.stringify({width:w,height:h,frames,fps:r.fps,duration:fx.meta.duration,loop:fx.meta.loop,clipped}));
 console.log(`${id}: ${r.n} frames, clipped=${clipped}`);
}else if(mode==='apply'){
 // Check every profile before replacing runtime assets.
 for(const p of settings.profiles){const result=await tunedRender(p.id,settings);if(result.clipped)throw Error(p.id+': pixels leave the frame. Reduce size before applying.');}
 process.env.PROJECTDM_PIXELFX_TUNING=path.resolve(file);
 await import('./projectdm-export.mjs');
 await import('./projectdm-preview.mjs');
 console.log('APPLIED');
}else throw Error('Expected preview or apply');
