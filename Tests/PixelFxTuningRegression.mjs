// Run from the repository root with Node.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {loadSettings,tunedRender} from '../work/pixelfx/tools/lib/projectdm-tuning.mjs';
const file='Design/Skills/PixelFX-v1/tuning.json',settings=loadSettings(file),before=fs.readFileSync(file);
const hash=r=>crypto.createHash('sha256').update(Buffer.concat(r.frames.map(f=>Buffer.from(f.clear)))).digest('hex');
const original=JSON.parse(fs.readFileSync('Design/Skills/PixelFX-v1/tuning-before.json'));
assert.equal(settings.profiles.length,15);
for(const p of settings.profiles){
 const {r,clipped}=await tunedRender(p.id,settings);assert.equal(clipped,0,p.id);
 if(!['missile','cone','blizzard','fireimpact'].includes(p.id))assert.equal(hash(r),original[p.id],p.id+' must preserve approved appearance');
}
const base=await tunedRender('cone',settings),changed=structuredClone(settings),p=changed.profiles.find(p=>p.id==='cone');
p.speed=2;p.density=.5;p.palette[3]='#ff0000';
const edited=await tunedRender('cone',changed);
assert.notEqual(hash(base.r),hash(edited.r));assert.equal(edited.r.n,6);assert.equal(edited.fx.meta.duration,.25);
const oversized=structuredClone(settings);oversized.profiles.find(p=>p.id==='beam').size=1.3;
assert.ok((await tunedRender('beam',oversized)).clipped>0,'Oversized effects must report lost pixels before applying');
const work=fs.mkdtempSync(path.resolve('Temp/PixelFXEditor/test-'));const snapshot=path.join(work,'tuning.json');
fs.writeFileSync(snapshot,JSON.stringify(changed));
const result=spawnSync('node',['tools/projectdm-edit.mjs','preview','cone',snapshot,work],{cwd:'work/pixelfx',encoding:'utf8'});
assert.equal(result.status,0,result.stderr);assert.equal(JSON.parse(fs.readFileSync(path.join(work,'preview.json'))).frames.length,6);
assert.ok(fs.statSync(path.join(work,'preview.png')).size>0);
p.speed=0;fs.writeFileSync(snapshot,JSON.stringify(changed));assert.throws(()=>loadSettings(snapshot),/speed/);
const invalid=spawnSync('node',['tools/projectdm-edit.mjs','apply','cone',snapshot,work],{cwd:'work/pixelfx',encoding:'utf8'});
assert.notEqual(invalid.status,0);assert.deepEqual(fs.readFileSync(file),before,'Preview must not overwrite saved tuning');
console.log('PIXELFX TUNING PASSED: 15 defaults, 11 approved appearances, live parameters, preview bridge and invalid-input rejection');
