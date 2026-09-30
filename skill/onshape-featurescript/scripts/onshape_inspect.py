#!/usr/bin/env python3
"""Lista os elementos de um documento Onshape e mede os corpos de cada Part Studio.

    python onshape_inspect.py --did <did> --wid <wid> [--only "nome do Part Studio"] > medidas.txt

Saida (mm, mundo Z-up), uma linha por item:
    B|i|nome|xmin,ymin,zmin|xmax,ymax,zmax|volume_mm3     corpo solido (bbox justa)
    C|i|raio|ax,ay,az|ox,oy,oz|area_mm2                   face cilindrica do corpo i
    P|i|nx,ny,nz|ox,oy,oz|area_mm2                        face plana do corpo i

Custo: 1 chamada para listar elementos + 1 por Part Studio medido (leitura).

REESCRITO em 2026-09-30: o original se perdeu com a maquina antiga. Mesmo formato
de saida (os scripts de projeto, ex. reassentar.py, leem as linhas B), mas ainda
NAO foi testado contra a API. Na primeira execucao, confira uma peca conhecida.

Credenciais: ONSHAPE_ACCESS_KEY / ONSHAPE_SECRET_KEY no ambiente.
Header Accept = application/json puro (a variante com qs=0.09 da 404 em rotas validas).
"""
import argparse
import json
import os
import sys

import requests

API = "https://cad.onshape.com/api/v10"
ACCESS = os.environ.get("ONSHAPE_ACCESS_KEY")
SECRET = os.environ.get("ONSHAPE_SECRET_KEY")

# Roda no servidor do Onshape. Monta o texto das linhas B/C/P e devolve uma string.
# Sem function aninhada (nao e permitido em FeatureScript); vetores formatados inline.
SCRIPT = r"""
function(context is Context, queries is map)
{
    var out = "";
    var bodies = evaluateQuery(context, qBodyType(qEverything(EntityType.BODY), BodyType.SOLID));
    for (var i = 0; i < size(bodies); i += 1)
    {
        var b = bodies[i];
        var nome = getProperty(context, { "entity" : b, "propertyType" : PropertyType.NAME });
        var box = evBox3d(context, { "topology" : b, "tight" : true });
        var mn = box.minCorner / millimeter;
        var mx = box.maxCorner / millimeter;
        var vol = evVolume(context, { "entities" : b }) / (millimeter ^ 3);
        out = out ~ "B|" ~ i ~ "|" ~ nome ~ "|" ~ mn[0] ~ "," ~ mn[1] ~ "," ~ mn[2]
                  ~ "|" ~ mx[0] ~ "," ~ mx[1] ~ "," ~ mx[2] ~ "|" ~ vol ~ "\n";

        var faces = evaluateQuery(context, qOwnedByBody(b, EntityType.FACE));
        for (var f in faces)
        {
            var s = evSurfaceDefinition(context, { "face" : f });
            var area = evArea(context, { "entities" : f }) / (millimeter ^ 2);
            if (s.surfaceType == SurfaceType.CYLINDER)
            {
                var ax = s.coordSystem.zAxis;
                var o = s.coordSystem.origin / millimeter;
                out = out ~ "C|" ~ i ~ "|" ~ (s.radius / millimeter) ~ "|" ~ ax[0] ~ "," ~ ax[1] ~ "," ~ ax[2]
                          ~ "|" ~ o[0] ~ "," ~ o[1] ~ "," ~ o[2] ~ "|" ~ area ~ "\n";
            }
            else if (s.surfaceType == SurfaceType.PLANE)
            {
                var n = s.normal;
                var o = s.origin / millimeter;
                out = out ~ "P|" ~ i ~ "|" ~ n[0] ~ "," ~ n[1] ~ "," ~ n[2]
                          ~ "|" ~ o[0] ~ "," ~ o[1] ~ "," ~ o[2] ~ "|" ~ area ~ "\n";
            }
        }
    }
    return out;
}
"""


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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--did", required=True)
    ap.add_argument("--wid", required=True)
    ap.add_argument("--only", default=None, help="medir so o Part Studio com este nome")
    args = ap.parse_args()

    if not ACCESS or not SECRET:
        sys.exit("ERRO: defina ONSHAPE_ACCESS_KEY e ONSHAPE_SECRET_KEY no ambiente.")
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    s = requests.Session()
    s.auth = (ACCESS, SECRET)
    h = {"Accept": "application/json", "Content-Type": "application/json"}

    def req(method, path, **kw):
        r = s.request(method, f"{API}{path}", headers=h, timeout=120, **kw)
        if not r.ok:
            sys.exit(f"[HTTP {r.status_code}] {method} {path}\n{r.text[:1500]}")
        return r.json()

    els = req("GET", f"/documents/d/{args.did}/w/{args.wid}/elements")
    print("=== elementos do documento ===")
    for e in els:
        print(f'  [{e["elementType"]:>12}] {e["name"]}  eid={e["id"]}')
    print()

    for e in els:
        if e["elementType"] != "PARTSTUDIO":
            continue
        if args.only and e["name"] != args.only:
            continue
        print(f'\n########## PART STUDIO: {e["name"]} (eid={e["id"]}) ##########')
        resp = req("POST", f'/partstudios/d/{args.did}/w/{args.wid}/e/{e["id"]}/featurescript',
                   data=json.dumps({"script": SCRIPT, "queries": {}}))
        texto = find_key(resp.get("result"), "value")
        if not isinstance(texto, str):
            notas = json.dumps(resp.get("notices") or resp)[:1500]
            print(f"# sem resultado em texto; resposta: {notas}")
            continue
        sys.stdout.write(texto)


if __name__ == "__main__":
    main()
