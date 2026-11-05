import fs from 'node:fs';import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {loadEffect,renderFrames} from './lib/fxlib.mjs';import {encodePNG} from './lib/png.mjs';
const root=path.resolve('../..'),ids=['idle','success','failure','condense-success','condense-failure'];
const records=[];let total=0;
for(const id of ids){const fx=await loadEffect('relic-forge/'+id),r=renderFrames(fx,{actor:false,bg:'none',options:{amount:0}});records.push({id,fx,r,first:total});total+=r.n;}
const cols=12,width=cols*160,height=Math.ceil(total/cols)*144,pixels=new Uint8Array(width*height*4),clips=[];
for(const {id,fx,r,first}of records){const frames=[];for(let i=0;i<r.n;i++){
 const x=(first+i)%cols*160,y=Math.floor((first+i)/cols)*144;
 for(let row=0;row<144;row++)pixels.set(r.frames[i].clear.subarray(row*160*4,(row+1)*160*4),((y+row)*width+x)*4);
 frames.push({x,y,width:160,height:144});
}clips.push({id,fps:r.fps,loop:fx.meta.loop,frames});}
const out=path.join(root,'Assets/Resources/ProjectDM/UI/RelicForge');fs.mkdirSync(out,{recursive:true});
fs.writeFileSync(path.join(out,'forge-effects.png'),encodePNG(width,height,pixels));
fs.writeFileSync(path.join(out,'forge-effects.json'),JSON.stringify({width,height,clips},null,2));
fs.copyFileSync('LICENSE',path.join(out,'LICENSE.txt'));
const preview=path.join(root,'Design/Relics/AlchemyWorkshop-v1');fs.mkdirSync(preview,{recursive:true});
for(const ext of ['png','json'])fs.copyFileSync(path.join(out,'forge-effects.'+ext),path.join(preview,'forge-effects.'+ext));
console.log(`Kettle atlas: ${clips.length} clips / ${total} frames, ${width}x${height}`);
// Match the UI's persistent idle layer, including the held crystal after a successful condensation.
const idle=records.find(r=>r.id==='idle').r;
async function previewGif(file,specs){
 const results=[];
 for(const spec of specs)results.push(renderFrames(await loadEffect('relic-forge/'+spec.id),{actor:false,bg:'none',options:{amount:spec.amount}}));
 const w=320*specs.length,h=288,n=65,raw=Buffer.alloc(w*h*n*3);
 for(let frame=0;frame<n;frame++)for(let panel=0;panel<specs.length;panel++)for(let y=0;y<144;y++)for(let x=0;x<160;x++){
  const at=(y*160+x)*4,color=[16,14,25];
  const over=rgba=>{const a=rgba[at+3]/255;for(let k=0;k<3;k++)color[k]=color[k]*(1-a)+rgba[at+k]*a;};
  over(idle.frames[frame%idle.n].clear);
  if(frame<results[panel].n)over(results[panel].frames[frame].clear);
  else if(specs[panel].id==='success'&&x>=65&&x<95&&y>=46&&y<80)over(results[panel].frames[26].clear);
  for(let yy=0;yy<2;yy++)for(let xx=0;xx<2;xx++){const dst=(frame*w*h+(y*2+yy)*w+panel*320+x*2+xx)*3;for(let k=0;k<3;k++)raw[dst+k]=color[k];}
 }
 const gif=spawnSync('ffmpeg',['-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s',w+'x'+h,'-r','24','-i','-','-filter_complex','split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none','-loop','0',path.join(preview,file)],{input:raw,maxBuffer:2e6});
 if(gif.status!==0)throw Error(gif.stderr.toString());
}
await previewGif('comparison.gif',[{id:'success',amount:100},{id:'failure',amount:100}]);
await previewGif('gold-amounts.gif',[10,1000,100000].map(amount=>({id:'success',amount})));

await previewGif('gold-pour-source.gif',[{id:'success',amount:1000}]);
const large=spawnSync('ffmpeg',['-v','error','-y','-i',path.join(preview,'gold-pour-source.gif'),'-filter_complex','scale=2448:1814:flags=neighbor,crop=1920:1080:264:500,scale=960:540:flags=neighbor,split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none','-loop','0',path.join(preview,'gold-pour-large.gif')],{maxBuffer:2e6});
if(large.status!==0)throw Error(large.stderr.toString());
