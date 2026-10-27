// Optional fixture regeneration from the community author's frozen current
// calculator. These are reverse-engineered rules, not official game formulas.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sourceCommit = 'ead9f319d18fb47e749718adac0d4384b785c0bc';
const sourceUrl = `https://raw.githubusercontent.com/hbr-tool/damage-calculator/${sourceCommit}/src/components/dmgCalc/logic.js`;
const expectedHash = '0e60cc885d9e48773dc36254116a8526912bed2f068c5973fe3505175a651f0c';

function extract(text, name) {
  const start = text.indexOf('export function ' + name + '(');
  if (start < 0) throw new Error(`Pinned source has no ${name}`);
  const open = text.indexOf('{', start);
  let nesting = 1, i = open + 1;
  for (; nesting && i < text.length; i++) {
    if (text[i] === '{') nesting++;
    else if (text[i] === '}') nesting--;
  }
  if (nesting) throw new Error(`Unbalanced function ${name}`);
  return text.slice(start, i).replace('export ', '');
}

async function main() {
  const response = await fetch(sourceUrl);
  if (!response.ok) throw new Error(`Pinned source download: HTTP ${response.status}`);
  const bytes = Buffer.from(await response.arrayBuffer());
  const hash = crypto.createHash('sha256').update(bytes).digest('hex');
  if (hash !== expectedHash) throw new Error(`Pinned source hash mismatch: ${hash}`);
  const text = bytes.toString('utf8');
  const attack = eval('(' + extract(text, 'calcAttackEffectSize') + ')');
  const buff = eval('(' + extract(text, 'calcBuffEffectSize') + ')');
  const debuff = eval('(' + extract(text, 'calcDebuffEffectSize') + ')');
  const result = [];
  const curves = [[100,200,100], [4005,8010,138], [4177.5,8355,144], [21,30,260]];
  for (const [min_power,max_power,param_limit] of curves) {
    for (const skillLv of [1,5,13]) {
      for (const difference of [-400,-145,-144,-138,-100,-1,0,1,50,100,138,144,200,400,1000]) {
        const parameter = 500 + difference, enemy = 500;
        const info = {min_power,max_power,param_limit};
        result.push({kind:'attack',minimum:min_power,maximum:max_power,limit:param_limit,parameter,enemy,level:skillLv,expected:attack(info,parameter,enemy,skillLv,0)});
        result.push({kind:'debuff',minimum:min_power,maximum:max_power,limit:param_limit,parameter,enemy,level:skillLv,expected:debuff(info,parameter,enemy,skillLv,0)});
      }
      for (const parameter of [0,1,50,100,138,144,200,260,400,1000]) {
        result.push({kind:'buff',minimum:min_power,maximum:max_power,limit:param_limit,parameter,enemy:0,level:skillLv,expected:buff(min_power,max_power,param_limit,parameter,skillLv,0)});
      }
    }
  }
  if (result.length !== 480 || result.some(v => !Number.isFinite(v.expected)))
    throw new Error('Invalid reference vector generation');
  fs.writeFileSync(path.join(__dirname, 'reference-vectors.json'), JSON.stringify(result,null,2));
  console.log(`Generated ${result.length} reference vectors from ${sourceCommit}`);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
