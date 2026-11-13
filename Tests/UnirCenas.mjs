// Reproduz a juncao por fileID e por propriedade, usando o ancestral comum real.
// Executar da raiz: node Tests/UnirCenas.mjs
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';

const dir = 'Assets/Cenario/PNB_Tropical_Jungle/Scenes/';
const ancestor = '47fe16365c154b4a673fd2d55be7f99cd28152c8';
const git = (...args) => execFileSync('git', args, { maxBuffer: 64 * 1024 * 1024 });
const pointer = git('show', `${ancestor}:${dir}Demo_Day.unity`).toString();
const oid = pointer.match(/oid sha256:(\w+)/)[1];
const lfs = git('rev-parse', '--git-path', `lfs/objects/${oid.slice(0,2)}/${oid.slice(2,4)}/${oid}`).toString().trim();
const baseText = fs.readFileSync(lfs, 'utf8');
const firstText = fs.readFileSync(dir + 'Demo_Day.unity', 'utf8');
const gorillaText = fs.readFileSync(dir + 'Demo_day_fase_gorilla.unity', 'utf8');

function parse(text) {
    const docs = new Map();
    for (const m of text.replaceAll('\r\n', '\n').matchAll(/^--- !u!(\d+) &(\d+)([^\n]*)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)) {
        assert(!docs.has(m[2]), `fileID repetido: ${m[2]}`);
        docs.set(m[2], { id: m[2], type: +m[1], suffix: m[3], body: m[4], raw: m[0] });
    }
    return docs;
}
function properties(doc) {
    return new Map([...doc.body.matchAll(/^  (\w+):[^\n]*\n(?:.*\n)*?(?=^  \w+:|$(?![\s\S]))/gm)].map(m => [m[1], m[0]]));
}
function mergeList(key, original, first, second) {
    const ids = text => [...(text ?? '').matchAll(/fileID: (\d+)/g)].map(m => m[1]);
    const base = ids(original), a = ids(first), b = ids(second);
    const result = [...new Set([...a, ...b])].filter(id => !base.includes(id) || (a.includes(id) && b.includes(id)));
    return `  ${key}:` + (result.length ? '\n' + result.map(id => `  - {fileID: ${id}}\n`).join('') : ' []\n');
}
const base = parse(baseText), first = parse(firstText), gorilla = parse(gorillaText), merged = new Map();
const report = { ancestor, addedFromGorilla: 0, changedFromGorilla: 0, removedFromGorilla: 0, combinedProperties: [] };
for (const id of new Set([...first.keys(), ...gorilla.keys(), ...base.keys()])) {
    const o = base.get(id), a = first.get(id), b = gorilla.get(id);
    let result;
    if (a?.raw === b?.raw || b?.raw === o?.raw) result = a;
    else if (a?.raw === o?.raw) {
        result = b;
        if (!o) report.addedFromGorilla++;
        else if (!b) report.removedFromGorilla++;
        else report.changedFromGorilla++;
    } else {
        assert(o && a && b, `Conflito de criacao/exclusao em ${id}`);
        const op = properties(o), ap = properties(a), bp = properties(b);
        let body = a.body.slice(0, a.body.indexOf('\n') + 1);
        for (const key of new Set([...ap.keys(), ...bp.keys()])) {
            const ov = op.get(key), av = ap.get(key), bv = bp.get(key);
            let value;
            if (av === bv || bv === ov) value = av;
            else if (av === ov) value = bv;
            else if (key === 'm_Children' || key === 'm_Roots') value = mergeList(key, ov, av, bv);
            else throw new Error(`Conflito nao resolvido: ${id}.${key}`);
            body += value ?? '';
        }
        result = { ...a, body, raw: `--- !u!${a.type} &${id}${a.suffix}\n${body}` };
        report.combinedProperties.push(id);
    }
    if (result) merged.set(id, result);
}
// O terreno sera combinado via TerrainData no Unity, em um asset independente.
const terrainGuid = 'c7790d5fd036470c8956cfe472f83ee1';
for (const [id, doc] of merged) {
    const raw = doc.raw.replace(/(m_TerrainData: \{fileID: 15600000, guid: )\w+/g, '$1' + terrainGuid);
    merged.set(id, { ...doc, raw });
}
const missing = [];
for (const doc of merged.values()) for (const m of doc.raw.matchAll(/\{fileID: (-?\d+)\}/g)) {
    if (m[1] !== '0' && !merged.has(m[1])) missing.push(`${doc.id} -> ${m[1]}`);
}
assert.equal(missing.length, 0, `Referencias locais quebradas: ${missing.join(', ')}`);
const output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + [...merged.values()]
    .sort((a, b) => BigInt(a.id) < BigInt(b.id) ? -1 : 1).map(d => d.raw).join('');
fs.mkdirSync('Logs/juncao', { recursive: true });
fs.writeFileSync('Logs/juncao/antes-da-juncao.unity', fs.readFileSync(dir + 'juncao_fase1-2.unity'));
fs.writeFileSync(dir + 'juncao_fase1-2.unity', output);
const terrainPath = 'Assets/Terrain/Demo_Day_Juncao.asset';
if (!fs.existsSync(terrainPath)) fs.copyFileSync('Assets/Terrain/Demo_Day_ExpandedCoast.asset', terrainPath);
fs.writeFileSync(terrainPath + '.meta', `fileFormatVersion: 2\nguid: ${terrainGuid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 15600000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
fs.writeFileSync('Assets/Terrain/TemporarioBaseJuncao.asset', git('show', `${ancestor}:Assets/Terrain/Demo_Day_ExpandedCoast.asset`));
report.documents = merged.size;
report.localReferencesMissing = missing.length;
fs.writeFileSync('Logs/juncao/merge-objetos.json', JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
