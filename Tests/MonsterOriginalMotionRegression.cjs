const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.join(__dirname,'../Design/Monsters/AttackPreview-v2');
const {motion}=require(path.join(root,'motion.js'));
for(let row=0;row<2;row++){
 assert.deepEqual(motion(row,0),{sx:1,sy:1,x:0,y:0});
 let changed=false;
 for(let frame=0;frame<260;frame++){const m=motion(row,frame/100);assert(m.sx>=1&&m.sx<=1.14);assert(m.sy>=.89&&m.sy<=1.11);assert(Math.abs(m.x)<=2);assert(Math.abs(m.y)<=2);changed ||= m.sx>1.01;}
 assert(changed);const end=motion(row,2.59);assert(Math.abs(end.sx-1)<.001&&Math.abs(end.sy-1)<.001);
}
const html=fs.readFileSync(path.join(root,'index.html'),'utf8'),script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const urls=JSON.parse(script.match(/const urls=(\[[^\n]+?\]),sprites=/)[1]);
const sources=['Meadow/3/Walk_Meadow_3_0.png','Dungeon/4/Walk_Dungeon_4_0.png'];
sources.forEach((p,i)=>assert.deepEqual(Buffer.from(urls[i].split(',')[1],'base64'),fs.readFileSync(path.join(__dirname,'../Assets/GameContent/Art/Pixel32/Monsters',p))));
let queue=[],draws=0,spriteDraws=0;const ctx=new Proxy({},{get:(o,k)=>o[k]??(()=>{draws++;if(k==='drawImage')spriteDraws++})}),elements=new Map();
const element=k=>{if(!elements.has(k))elements.set(k,{checked:true,getContext:()=>ctx});return elements.get(k)};
const sandbox={Image:class{set src(v){assert(v.startsWith('data:image/png;base64,'));this.onload()}},document:{querySelector:element},requestAnimationFrame:f=>queue.push(f)};
vm.createContext(sandbox);vm.runInContext(script,sandbox);assert.equal(queue.length,1);
for(let i=1;i<100;i++)queue.shift()(i*30);assert.equal(spriteDraws,198);assert(draws>spriteDraws);assert(vm.runInContext('time>2',sandbox));
assert(!html.includes('id="effects"'));assert(!script.includes('ellipse('));for(let i=100;i<150;i++)queue.shift()(i*30);
element('#play').onclick();const paused=vm.runInContext('time',sandbox);queue.shift()(4510);assert.equal(vm.runInContext('time',sandbox),paused);
console.log('Original monster motion PASS: source PNG byte parity, subtle bounded transforms, recovery, cached loading, body-only animation, no indicators, pause.');
