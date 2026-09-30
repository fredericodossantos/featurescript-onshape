#!/usr/bin/env python3
"""Renderiza um Part Studio ou uma Assembly em varios angulos (PNG), sem alterar nada.

    python onshape_render.py --did <did> --wid <wid> --eid <eid> \
        [--kind partstudio|assembly] [--views iso,top,front,right] [--out out] [--prefix view]

Custo: 1 chamada de leitura por vista. Depois de renderizar, ABRA os PNG:
featureStatus=OK nao garante geometria correta.

Credenciais: ONSHAPE_ACCESS_KEY / ONSHAPE_SECRET_KEY no ambiente.
"""
import argparse
import base64
import os
import sys
from pathlib import Path

import requests

API = "https://cad.onshape.com/api/v10"

# viewMatrix 3x4 row-major, mundo Z-up (mesmas vistas do onshape_build_into.py)
VIEWS = {
    "iso": "0.612,0.612,0,0,-0.354,0.354,0.707,0,0.707,-0.707,0.707,0",
    "top": "1,0,0,0,0,1,0,0,0,0,1,0",
    "front": "1,0,0,0,0,0,1,0,0,-1,0,0",
    "right": "0,1,0,0,0,0,1,0,1,0,0,0",
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--did", required=True)
    ap.add_argument("--wid", required=True)
    ap.add_argument("--eid", required=True)
    ap.add_argument("--kind", choices=["partstudio", "assembly"], default="partstudio")
    ap.add_argument("--views", default="iso,top,front")
    ap.add_argument("--out", default="out")
    ap.add_argument("--prefix", default="view")
    ap.add_argument("--size", default="1400x1000", help="LARGURAxALTURA em pixels")
    args = ap.parse_args()

    access, secret = os.environ.get("ONSHAPE_ACCESS_KEY"), os.environ.get("ONSHAPE_SECRET_KEY")
    if not access or not secret:
        sys.exit("ERRO: defina ONSHAPE_ACCESS_KEY e ONSHAPE_SECRET_KEY no ambiente.")
    w, h = (int(v) for v in args.size.lower().split("x"))

    s = requests.Session()
    s.auth = (access, secret)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    rota = "partstudios" if args.kind == "partstudio" else "assemblies"

    for name in [v.strip() for v in args.views.split(",") if v.strip()]:
        vm = VIEWS.get(name)
        if vm is None:
            print(f"[{name}] vista desconhecida; opcoes: {', '.join(VIEWS)}")
            continue
        r = s.get(f"{API}/{rota}/d/{args.did}/w/{args.wid}/e/{args.eid}/shadedviews",
                  params={"viewMatrix": vm, "outputWidth": w, "outputHeight": h,
                          "pixelSize": 0, "edges": "show", "useAntiAliasing": True},
                  headers={"Accept": "application/json"}, timeout=120)
        if not r.ok:
            print(f"[{name}] HTTP {r.status_code}: {r.text[:300]}")
            continue
        imgs = r.json().get("images") or []
        if not imgs:
            print(f"[{name}] resposta sem imagem")
            continue
        p = out / f"{args.prefix}_{name}.png"
        p.write_bytes(base64.b64decode(imgs[0]))
        print(f"[{name}] -> {p}")


if __name__ == "__main__":
    main()
