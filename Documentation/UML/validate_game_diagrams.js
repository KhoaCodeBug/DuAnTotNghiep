const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const filePath = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.join(__dirname, 'Fragments_of_Survival_UML_Professional_v2.drawio');
const expectedPages = Number(process.argv[3] || 19);
const file = fs.readFileSync(filePath, 'utf8');
const diagrams = [...file.matchAll(/<diagram id="([^"]+)" name="([^"]+)">([\s\S]*?)<\/diagram>/g)];

if (diagrams.length !== expectedPages) throw new Error(`Expected ${expectedPages} pages, got ${diagrams.length}`);

let totalVertices = 0;
let totalEdges = 0;

for (const [, id, name, payload] of diagrams) {
  const xml = decodeURIComponent(zlib.inflateRawSync(Buffer.from(payload, 'base64')).toString('utf8'));
  if (!xml.startsWith('<mxGraphModel') || !xml.endsWith('</mxGraphModel>'))
    throw new Error(`${name}: invalid mxGraphModel payload`);

  const ids = [...xml.matchAll(/<mxCell id="([^"]+)"/g)].map(match => match[1]);
  const unique = new Set(ids);
  if (ids.length !== unique.size) throw new Error(`${name}: duplicate cell ids`);
  if (!unique.has('0') || !unique.has('1')) throw new Error(`${name}: missing root cells`);

  for (const match of xml.matchAll(/<mxCell[^>]+edge="1"[^>]+source="([^"]+)" target="([^"]+)"/g)) {
    if (!unique.has(match[1]) || !unique.has(match[2]))
      throw new Error(`${name}: broken edge ${match[1]} -> ${match[2]}`);
  }

  const vertices = (xml.match(/vertex="1"/g) || []).length;
  const edges = (xml.match(/edge="1"/g) || []).length;
  if (vertices === 0 || edges === 0) throw new Error(`${name}: blank or disconnected page`);

  if (!name.includes('Activity Flow')) {
    if (!xml.includes('shape=umlActor')) throw new Error(`${name}: missing stick-figure actor`);
    if (!xml.includes('ellipse;whiteSpace=wrap')) throw new Error(`${name}: missing use-case ellipses`);
  }

  totalVertices += vertices;
  totalEdges += edges;
  console.log(`${id} | ${name} | vertices=${vertices} edges=${edges}`);
}

console.log(`VALID: ${diagrams.length} pages, ${totalVertices} vertices, ${totalEdges} edges`);
