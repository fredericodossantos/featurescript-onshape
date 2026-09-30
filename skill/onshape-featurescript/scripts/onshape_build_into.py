#!/usr/bin/env python3
"""Sobe um .fs para um documento QUE JA EXISTE e instancia o feature num Part Studio.

Diferenca para o onshape_build.py: nao cria documento. Serve para trabalhar dentro
do documento do usuario (o link que ele mandou), sem espalhar documentos novos.

    python onshape_build_into.py --did <did> --wid <wid> --fs modelo.fs \
        [--ps <eid_part_studio>] [--fsname "Mesa XY"] [--out out]

- Cria um Feature Studio novo (ou reusa o de mesmo nome, com --reuse).
- Sincroniza a versao da std lib lendo o proprio Feature Studio criado.
- Instancia o feature no Part Studio indicado (ou no primeiro do documento).
- Renderiza iso/front/top e grava os PNG em --out.

ARMADILHA JA PAGA: o header Accept "application/json;charset=UTF-8; qs=0.09" faz
varias rotas responderem 404 "Not found". Use application/json puro.
"""
import argparse
import base64
import json
import os
import re
import sys
from pathlib import Path

import requests

API = "https://cad.onshape.com/api/v10"
BASE = "https://cad.onshape.com"
ACCESS = os.environ.get("ONSHAPE_ACCESS_KEY")
SECRET = os.environ.get("ONSHAPE_SECRET_KEY")

ap = argparse.ArgumentParser()
ap.add_argument("--did", required=True)
ap.add_argument("--wid", required=True)
ap.add_argument("--fs", required=True, help="caminho do .fs")
ap.add_argument("--ps", default=None, help="eid do Part Studio alvo")
ap.add_argument("--fsname", default=None, help="nome do Feature Studio (default: nome do arquivo)")
ap.add_argument("--reuse", action="store_true", help="reusar Feature Studio de mesmo nome, se existir")
ap.add_argument("--params", default=None,
                help="JSON com os parametros do feature: lista de {id,type,value[,enumName]}, "
                     "type em length|int|bool|enum. SEM isso o feature regenera com "
                     "parametros indefinidos e da featureStatus=ERROR")
ap.add_argument("--replace", action="store_true",
                help="apagar instancias anteriores do mesmo featureType antes de inserir")
ap.add_argument("--update", action="store_true",
                help="ATUALIZAR a instancia existente em vez de apagar e recriar. "
                     "Preserva os partId, e com isso as instancias de qualquer ASSEMBLY "
                     "que use essas pecas continuam validas. Com --replace os partId mudam "
                     "e a montagem fica com instancias orfas (ja aconteceu).")
ap.add_argument("--out", default="out")
args = ap.parse_args()

if not ACCESS or not SECRET:
    sys.exit("ERRO: defina ONSHAPE_ACCESS_KEY e ONSHAPE_SECRET_KEY no ambiente.")

fs_path = Path(args.fs).resolve()
fs_src = fs_path.read_text(encoding="utf-8")
mt = re.search(r"export\s+const\s+(\w+)\s*=\s*defineFeature", fs_src)
if not mt:
    sys.exit("ERRO: nao achei 'export const <nome> = defineFeature' no .fs")
feature_type = mt.group(1)
fs_name = args.fsname or fs_path.stem

out_dir = Path(args.out).resolve()
out_dir.mkdir(parents=True, exist_ok=True)

S = requests.Session()
S.auth = (ACCESS, SECRET)
H = {"Accept": "application/json", "Content-Type": "application/json"}


def req(method, path, **kw):
    r = S.request(method, f"{API}{path}", headers=H, timeout=90, **kw)
    if not r.ok:
        sys.exit(f"\n[HTTP {r.status_code}] {method} {path}\n{r.text[:1500]}\n")
    return r


def find_key(o, k):
    if isinstance(o, dict):
        if k in o:
            return o[k]
        for v in o.values():
            r = find_key(v, k)
            if r is not None:
                return r
    elif isinstance(o, list):
        for v in o:
            r = find_key(v, k)
            if r is not None:
                return r
    return None


did, wid = args.did, args.wid

print("=== 1/6 elementos ===")
els = req("GET", f"/documents/d/{did}/w/{wid}/elements").json()
for e in els:
    print(f'  {e["elementType"]:15} {e["id"]}  {e["name"]}')

eid_ps = args.ps or next(e["id"] for e in els if e["elementType"] == "PARTSTUDIO")

print("=== 2/6 Feature Studio ===")
eid_fs = None
if args.reuse:
    eid_fs = next((e["id"] for e in els
                   if e["elementType"] == "FEATURESTUDIO" and e["name"] == fs_name), None)
if eid_fs is None:
    eid_fs = req("POST", f"/featurestudios/d/{did}/w/{wid}",
                 data=json.dumps({"name": fs_name})).json()["id"]
    print(f"  criado {eid_fs}")
else:
    print(f"  reusando {eid_fs}")

print("=== 3/6 versao da std lib ===")
fsget = req("GET", f"/featurestudios/d/{did}/w/{wid}/e/{eid_fs}").json()
ser_ver = fsget.get("serializationVersion", "1.2.0")
src_mv = fsget.get("sourceMicroversion", "") or find_key(fsget, "microversionId") or ""
mv = re.search(r"FeatureScript\s+(\d+);", fsget.get("contents", "") or "")
std_ver = mv.group(1) if mv else "3083"
print(f"  stdLib={std_ver}")

print("=== 4/6 subindo codigo ===")
src = re.sub(r"FeatureScript\s+\d+;", f"FeatureScript {std_ver};", fs_src, count=1)
src = re.sub(r'version\s*:\s*"\d+\.0"', f'version : "{std_ver}.0"', src)
req("POST", f"/featurestudios/d/{did}/w/{wid}/e/{eid_fs}", data=json.dumps({
    "contents": src, "serializationVersion": ser_ver,
    "sourceMicroversion": src_mv, "rejectMicroversionSkew": False}))
print(f"  feature '{feature_type}' enviado ({len(src)} bytes)")

print("=== 5/6 instanciando ===")
els2 = req("GET", f"/documents/d/{did}/w/{wid}/elements").json()
fs_mv = next((e.get("microversionId") for e in els2 if e["id"] == eid_fs), None)
ns = f"e{eid_fs}::m{fs_mv}"


def build_params(spec, namespace):
    """Serializa os parametros no formato BTM. Sem isso o feature nasce com
    definition.* == undefined (o default do dialogo so vale na UI)."""
    out = []
    for p in spec:
        t = p["type"]
        if t in ("length", "angle", "int", "real"):
            out.append({"btType": "BTMParameterQuantity-147",
                        "parameterId": p["id"], "expression": str(p["value"])})
        elif t == "bool":
            out.append({"btType": "BTMParameterBoolean-144",
                        "parameterId": p["id"], "value": bool(p["value"])})
        elif t == "enum":
            out.append({"btType": "BTMParameterEnum-145", "parameterId": p["id"],
                        "value": p["value"], "enumName": p["enumName"], "namespace": namespace})
        else:
            sys.exit(f"tipo de parametro desconhecido: {t}")
    return out


params = []
if args.params:
    params = build_params(json.loads(Path(args.params).read_text(encoding="utf-8")), ns)
    print(f"  {len(params)} parametros")
else:
    print("  AVISO: sem --params; o feature pode dar ERROR por parametro indefinido")

fid_existente = None
if args.update:
    cur = req("GET", f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features").json()
    for f in cur.get("features", []):
        m = f.get("message", f)
        if m.get("featureType") == feature_type:
            fid_existente = m.get("featureId")
    print(f"  atualizando feature existente {fid_existente}" if fid_existente
          else "  nenhuma instancia previa; sera inserida")

if args.replace and not args.update:
    cur = req("GET", f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features").json()
    for f in cur.get("features", []):
        m = f.get("message", f)
        if m.get("featureType") == feature_type:
            fid = m.get("featureId")
            req("DELETE", f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features/featureid/{fid}")
            print(f"  removida instancia anterior {fid}")

feat = {"btType": "BTMFeature-134", "featureType": feature_type, "name": feature_type,
        "namespace": ns, "parameters": params, "suppressed": False,
        "returnAfterSubfeatures": False, "subFeatures": []}
if fid_existente:
    feat["featureId"] = fid_existente
    rota = f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features/featureid/{fid_existente}"
else:
    rota = f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features"
resp = req("POST", rota, data=json.dumps({
    "btType": "BTFeatureDefinitionCall-1406", "feature": feat})).json()
status = find_key(resp, "featureStatus")
print(f"  namespace={ns}\n  featureStatus={status}")
if status is not None and str(status).upper() not in ("OK", "NONE"):
    msgs = json.dumps(resp)
    for m in re.findall(r'"message"\s*:\s*"([^"]{5,300})"', msgs):
        print("   !", m)

(out_dir / "last_doc.json").write_text(json.dumps(
    {"did": did, "wid": wid, "eid_ps": eid_ps, "eid_fs": eid_fs, "feature": feature_type},
    indent=2), encoding="utf-8")

print("=== 6/6 render ===")
VIEWS = {
    "iso": "0.612,0.612,0,0,-0.354,0.354,0.707,0,0.707,-0.707,0.707,0",
    "top": "1,0,0,0,0,1,0,0,0,0,1,0",
    "front": "1,0,0,0,0,0,1,0,0,-1,0,0",
}
for name, vm in VIEWS.items():
    r = req("GET", f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/shadedviews", params={
        "viewMatrix": vm, "outputWidth": 1400, "outputHeight": 1000,
        "pixelSize": 0, "edges": "show", "useAntiAliasing": True})
    imgs = r.json().get("images") or []
    if imgs:
        p = out_dir / f"view_{name}.png"
        p.write_bytes(base64.b64decode(imgs[0]))
        print(f"  {p}")

print(f"\nABRA: {BASE}/documents/{did}/w/{wid}/e/{eid_ps}")
print("Confira as imagens. featureStatus=OK nao garante geometria correta.")
