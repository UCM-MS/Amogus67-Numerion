// Unity's gzip payload stays byte-for-byte identical. Small assets fit the host's file limit.
export function validateDataManifest(manifest) {
  if (!manifest || manifest.schema !== 1 || manifest.filename !== 'WebGL.data.unityweb' ||
      !Number.isSafeInteger(manifest.bytes) || manifest.bytes < 1 || manifest.bytes > 512 * 1024 * 1024 ||
      !Array.isArray(manifest.parts) || manifest.parts.length < 1 || manifest.parts.length > 32)
    throw new Error('Neplatný zoznam herných dát.');
  let total = 0;
  const seen = new Set();
  for (const part of manifest.parts) {
    if (!/^numerion-data-[a-f0-9]{16}-\d{2}\.bin$/.test(part?.file || '') || seen.has(part.file) ||
        !Number.isSafeInteger(part.bytes) || part.bytes < 1 || part.bytes > 20 * 1024 * 1024 ||
        !/^sha256-[A-Za-z0-9+/]{43}=$/.test(part.integrity || '')) throw new Error('Neplatná časť herných dát.');
    seen.add(part.file); total += part.bytes;
  }
  if (total !== manifest.bytes) throw new Error('Nesúhlasí veľkosť herných dát.');
  return manifest;
}

export async function prepareNumerionData(originalUrl, onProgress, fetchFile = fetch) {
  const original = new URL(originalUrl, location.href);
  const response = await fetchFile(new URL('data-manifest.json' + original.search, original), {cache:'no-cache'});
  if (response.status === 404) return {url:original.href, release(){}};
  if (!response.ok) throw new Error('Herné dáta sa nepodarilo načítať.');
  const manifest = validateDataManifest(await response.json()), blobs = [];
  let received = 0;
  for (const part of manifest.parts) {
    const url = new URL(part.file, original);
    const result = await fetchFile(url, {cache:'force-cache', integrity:part.integrity});
    if (!result.ok) throw new Error('Chýba časť herných dát. Obnov stránku.');
    const blob = await result.blob();
    if (blob.size !== part.bytes) throw new Error('Stiahnuté herné dáta nie sú úplné.');
    blobs.push(blob); received += blob.size; onProgress?.(received / manifest.bytes);
  }
  const url = URL.createObjectURL(new Blob(blobs, {type:'application/octet-stream'}));
  return {url, release(){URL.revokeObjectURL(url);}};
}
