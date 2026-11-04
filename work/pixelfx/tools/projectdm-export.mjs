import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {loadEffect,renderFrames,allIds} from './lib/fxlib.mjs';
import {encodePNG} from './lib/png.mjs';
import {loadSettings,tunedRender} from './lib/projectdm-tuning.mjs';
const root=path.resolve('../..'),out=path.join(root,'Assets/Resources/ProjectDM/Skills/PixelFX');fs.mkdirSync(out,{recursive:true});
const ids=allIds().filter(id=>id.startsWith('projectdm/'));
const settings=loadSettings(process.env.PROJECTDM_PIXELFX_TUNING??path.join(root,'Design/Skills/PixelFX-v1/tuning.json'));
const tile=100,records=[];let total=0;
for(const id of ids){const {fx,r,clipped}=await tunedRender(id.split('/')[1],settings);if(clipped)throw Error(id+': clipped pixels');records.push({id:id.split('/')[1],fx,r,first:total});total+=r.n;}
const columns=Math.max(12,Math.ceil(total/20)),width=columns*tile;
const height=Math.ceil(total/columns)*tile,atlas=new Uint8Array(width*height*4),clips=[];
for(const {id,fx,r,first} of records){const rects=[];
 for(let i=0;i<r.n;i++){let x=((first+i)%columns)*tile+2,y=Math.floor((first+i)/columns)*tile+2;
  for(let py=0;py<r.h;py++)atlas.set(r.frames[i].clear.subarray(py*r.w*4,(py+1)*r.w*4),((y+py)*width+x)*4);
  rects.push({x,y,width:r.w,height:r.h});
 }
 clips.push({id,fps:r.fps,loop:fx.meta.loop,frames:rects});
}
fs.writeFileSync(path.join(out,'skill-effects.png'),encodePNG(width,height,atlas));
fs.writeFileSync(path.join(out,'skill-effects.json'),JSON.stringify({width,height,clips},null,2));
const preview=path.join(root,'Design/Skills/PixelFX-v1');fs.mkdirSync(preview,{recursive:true});
fs.copyFileSync(path.join(out,'skill-effects.png'),path.join(preview,'skill-effects.png'));
fs.copyFileSync(path.join(out,'skill-effects.json'),path.join(preview,'skill-effects.json'));
fs.copyFileSync('LICENSE',path.join(out,'LICENSE.txt'));
function decode(file,w,h){const p=spawnSync('ffmpeg',['-v','error','-i',file,'-vf',`scale=${w}:${h}:flags=neighbor`,'-f','rawvideo','-pix_fmt','rgba','-'],{maxBuffer:40e6});if(p.status!==0)throw Error(p.stderr.toString());return p.stdout;}
const backgrounds=['meadow','dungeon'].map(n=>decode(path.join(root,`Docs/QA/2026-11-03-${n}-sharp.png`),560,300));
// The scene boards use actual game screenshots, with a single effect at each comparable preview size.
for(let biome=0;biome<2;biome++){
 const w=960,h=640,board=new Uint8Array(w*h*4);
 for(let i=0;i<records.length;i++){
  const {r}=records[i],cellx=i%4*240,celly=Math.floor(i/4)*160;
  for(let y=0;y<160;y++)for(let x=0;x<240;x++){const src=((70+y)%300*560+(140+x)%560)*4,at=((celly+y)*w+cellx+x)*4;board.set(backgrounds[biome].subarray(src,src+4),at);}
  let frame=r.frames[Math.min(r.n-2,Math.floor(r.n*.3))].clear;
  const scale=2,left=cellx+120-Math.floor(r.w*scale/2),top=celly+80-Math.floor(r.h*scale/2);
  for(let y=0;y<r.h;y++)for(let x=0;x<r.w;x++){const src=(y*r.w+x)*4,a=frame[src+3]/255;if(a===0)continue;for(let yy=0;yy<scale;yy++)for(let xx=0;xx<scale;xx++){let px=left+x*scale+xx,py=top+y*scale+yy;if(px<cellx||px>=cellx+240||py<celly||py>=celly+160)continue;let dst=(py*w+px)*4;for(let k=0;k<3;k++)board[dst+k]=frame[src+k]*a+board[dst+k]*(1-a);board[dst+3]=255;}}
 }
 fs.writeFileSync(path.join(preview,`${biome?'dungeon':'meadow'}-board.png`),encodePNG(w,h,board));
}
console.log(`Packed ${clips.length} clips / ${total} frames into ${width}x${height}, one shared texture.`);
