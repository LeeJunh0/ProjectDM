import fs from 'node:fs';
import path from 'node:path';
import {palettes} from '../src/fx/projectdm/_shared.js';
const effects={missile:'arcane',beam:'arcane',satellite:'arcane',cone:'fire',firefield:'fire',meteor:'fire',chain:'electric',orb:'arcane',thunder:'electric',frostshard:'ice',blizzard:'ice',shatter:'ice',hit:'arcane',mote:'arcane',fireimpact:'fire'};
const revision=Number(process.argv[2]??3);
for(const [name,palette] of Object.entries(effects)){
 const size=name==='beam'||name==='chain'?[96,32]:name==='cone'?[96,64]:[96,96];
 const origin=name==='cone'?[12,32]:name==='missile'?[22,48]:[size[0]/2,size[1]/2];
 const loop=!['shatter','hit','fireimpact'].includes(name);
 const colors=name==='missile'?['#26064f','#50129d','#8929dc','#b564ed','#e7b6ff']:(name==='mote'||name==='hit')?['#253147','#72839a','#b6c6d2','#e0e8ee','#ffffff']:palettes[palette];
 const code=`import {drawEffect} from './_shared.js';\nexport const meta=${JSON.stringify({name:'Project DM '+name,desc:'Game-scale pixel '+name+'; sparse, palette-matched, no gameplay changes.',tags:['projectdm',palette],size,origin,duration:.5,fps:24,loop,bg:'ground',options:{palette:colors,seed:17,motif:name,revision}})};\nexport const draw=drawEffect;\n`;
 fs.writeFileSync(path.resolve('src/fx/projectdm',name+'.js'),code);
}
console.log('Project DM effect sources revision',revision);
