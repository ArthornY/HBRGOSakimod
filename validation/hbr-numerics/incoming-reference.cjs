// Offline fixture regeneration. No downloaded source is imported wholesale.
// The primary oracle is a declared transcription of Cantata Kamen's diagram;
// the frozen original Roy function is evaluated separately to expose disagreement.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const root = path.join(__dirname, 'incoming-reference');
const metadata = JSON.parse(fs.readFileSync(path.join(root, 'sources.json'), 'utf8'));
for (const [name, expected] of Object.entries(metadata.files)) {
  const hash = crypto.createHash('sha256').update(fs.readFileSync(path.join(root, name))).digest('hex');
  if (hash !== expected) throw new Error('Frozen incoming source hash mismatch: ' + name);
}
const primary = require('./incoming-reference/cantata-incoming.cjs');
const source = fs.readFileSync(path.join(root, 'roy-incoming.ts'), 'utf8');
const signature = 'export function calcIncomingDamage(input: IncomingDamageInput): IncomingDamageResult {';
const start = source.indexOf(signature);
if (start < 0) throw new Error('Frozen original incoming function not found');
const original = source.slice(start).trim()
  .replace(signature, 'function calcIncomingDamage(input) {')
  .replace('let skillPower: number;', 'let skillPower;');
if (original.includes(': number') || !original.endsWith('}')) throw new Error('Unexpected TypeScript function shape');
const compare = vm.runInNewContext('(' + original + ')', {}, { timeout: 1000 });
const curves = [[100,200,100],[1000,2000,100],[400,7080,200],
  [2970,13070,1500],[12.345,98.765,6.25],[1,100,1]];
const ratios = [-2,-1.001,-1,-0.9995,-0.99,-0.5,-0.1,-0.001,0,0.001,0.1,0.5,0.999,1,1.001,2];
const vectors = [];
function add(name, enemy, defense, curve, weighted) {
  const [minimum, maximum, limit] = curve;
  const input = {vit: defense, spr: defense, biasType:'hp', enemyBorder:enemy,
    enemyPerc:1, skillMin:minimum, skillMax:maximum, skillDiff:limit,
    mark:false, necklace:false, passiveDef:0};
  if (weighted) Object.assign(input, weighted);
  const compared = compare(input);
  const defensive = compared.biasValue;
  const expected = primary(enemy, defensive, minimum, maximum, limit);
  if (!Number.isFinite(expected) || !Number.isFinite(compared.skillPower))
    throw new Error('Invalid reference result: ' + name);
  vectors.push({name,enemyParameter:enemy,defensiveParameter:defensive,
    minimum,maximum,limit,expected,comparedRoy:compared.skillPower,
    ...(weighted ? {weightedInput:weighted} : {})});
}
for (const curve of curves)
  for (const ratio of ratios)
    add('curve ' + curve.join('/') + ' difference ratio ' + ratio,
      4000, 4000 - ratio * curve[2], curve);
for (const biasType of ['hp','dp']) {
  for (const [vit,spr] of [[510,390],[390,510],[125.25,375.75],[375.75,125.25]]) {
    add('weighted ' + biasType + ' ' + vit + ':' + spr,
      400, 0, [1000,2000,100], {vit,spr,biasType});
  }
}
const differences = vectors.filter(v => Math.abs(v.expected - v.comparedRoy) > 0.0000001).length;
if (vectors.length !== 104 || differences === 0) throw new Error('Invalid incoming fixture coverage');
const fixture = {primarySource:metadata.primary.article,
  primaryImageDate:metadata.primary.formula.imageDate,
  comparedSourceCommit:metadata.compared.commit,
  oracle:'cantata-incoming.cjs: declared transcription; comparedRoy: frozen original calcIncomingDamage',
  tolerance:0.0000001,documentedRoyDifferences:differences,vectors};
fs.writeFileSync(path.join(__dirname, 'incoming-vectors.json'), JSON.stringify(fixture,null,2) + '\n');
console.log(JSON.stringify({generated:vectors.length,documentedRoyDifferences:differences,
  primaryDate:fixture.primaryImageDate,comparedCommit:fixture.comparedSourceCommit}));
