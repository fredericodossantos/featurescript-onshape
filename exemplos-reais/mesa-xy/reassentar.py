"""Reassenta a montagem: compara a posicao REAL de cada instancia com a posicao
de projeto e aplica a correcao. Serve para quando alguem arrasta uma peca sem
querer - e tambem como verificacao, porque imprime o desvio de cada uma.

A referencia de cada peca (o ponto dela que corresponde a origem da maquina)
vem da bounding box medida, igual ao que a montagem usou para posicionar.
"""
import base64, json, os, sys, urllib.request, urllib.error

D = "ef2faa3446bb2591886d7fa9"
W = "e99d15a7af85fd99690a70fc"
ASM = "9304b9ba04e2ea59cc7bb5e9"
MED = "med12.txt"
auth = base64.b64encode(f'{os.environ["ONSHAPE_ACCESS_KEY"]}:{os.environ["ONSHAPE_SECRET_KEY"]}'.encode()).decode()


def call(m, p, b=None):
    r = urllib.request.Request(f"https://cad.onshape.com/api/v10{p}", method=m,
                               data=json.dumps(b).encode() if b else None)
    r.add_header("Authorization", "Basic " + auth)
    r.add_header("Accept", "application/json")
    if b:
        r.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(r) as x:
            return json.loads(x.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        sys.exit(f"{e.code} {m} {p}: {e.read().decode()[:300]}")


bb = {}
for ln in open(MED, encoding="utf-8"):
    if ln.startswith("B|"):
        p = ln.strip().split("|")
        bb[p[2]] = ([float(v) for v in p[3].split(",")], [float(v) for v in p[4].split(",")])

Z_EIXO, Z_CARRO, Z_MDF = 20.0, 30.5, 53.5
# nome da peca -> (regra de referencia, lista de posicoes de projeto)
PROJETO = {
    "01_Carro_simples_x1":            (lambda a, b: (a[0] + 25, 0.0, a[2] + 10.5), [(-50, 0, Z_EIXO)]),
    "01B_Carro_com_aba_castanha_x1":  (lambda a, b: (a[0] + 25, a[1] + 70.5, b[2] - 10.5), [(50, 0, Z_EIXO)]),
    "02A_Suporte_base_x2":            (lambda a, b: (a[0] + 21, 0.0, a[2]), [(-50, 0, Z_CARRO), (50, 0, Z_CARRO)]),
    "02B_Suporte_tampa_x2":           (lambda a, b: (a[0] + 21, 0.0, a[2] - 10.7), [(-50, 0, Z_CARRO), (50, 0, Z_CARRO)]),
    "03_Mancal_base_eixo_X_x4":       (lambda a, b: (a[0] + 10, 0.0, a[2]),
                                       [(-190, 60, 0), (190, 60, 0), (-190, -60, 0), (190, -60, 0)]),
    "04_Mancal_mesa_eixo_Y_x2":       (lambda a, b: ((a[0] + b[0]) / 2, 0.0, b[2]), [(0, 190, Z_MDF), (0, -190, Z_MDF)]),
    "05_Suporte_motor_NEMA17_x1":     (lambda a, b: (a[0] + 25, 0.0, a[2]), [(230, 90, 0)]),
    "06_Mancal_ponta_fuso_608_x1":    (lambda a, b: (a[0], 0.0, a[2]), [(-90, 90, 0)]),
    "07_Mesa_MDF_CORTAR_nao_imprimir_x1": (lambda a, b: ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, a[2]), [(0, 0, Z_MDF)]),
    "08_Base_MDF_gabarito_furacao_x1": (lambda a, b: ((a[0] + b[0]) / 2, a[1] + 90, b[2]), [(0, 0, 0)]),
    "09_Ponte_grampo_correia_Y_x1":   (lambda a, b: ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, a[2]), [(0, 0, Z_CARRO)]),
}

root = call("GET", f"/assemblies/d/{D}/w/{W}/e/{ASM}")["rootAssembly"]
nm = {i["id"]: i.get("name", "") for i in root["instances"]}

atual = {}
for oc in root["occurrences"]:
    if len(oc["path"]) != 1:
        continue
    n = nm.get(oc["path"][0], "")
    base = n.split(" <")[0]
    if base not in bb:
        continue
    t = oc["transform"]
    # posicao atual do ponto de referencia da peca
    a, b = bb[base]
    d3 = [t[3] * 1000, t[7] * 1000, t[11] * 1000]
    regra = PROJETO[base][0]
    o = regra(a, b)
    atual.setdefault(base, []).append((n, oc["path"][0], [o[k] + d3[k] for k in range(3)]))

total_desvio = 0
correcoes = []
for base, lista in atual.items():
    alvos = list(PROJETO[base][1])
    # casa cada instancia com o alvo mais proximo (pecas iguais sao permutaveis)
    for n, pid, pos in sorted(lista, key=lambda z: z[0]):
        melhor = min(alvos, key=lambda al: sum((al[k] - pos[k]) ** 2 for k in range(3)))
        alvos.remove(melhor)
        d = [melhor[k] - pos[k] for k in range(3)]
        desvio = max(abs(v) for v in d)
        marca = ""
        if desvio > 0.05:
            correcoes.append((n, pid, d))
            marca = "  <== fora de lugar"
            total_desvio += 1
        print(f"  {n:40} desvio {[round(v, 2) for v in d]}{marca}")

print(f"\ninstancias fora de lugar: {total_desvio}")
for n, pid, d in correcoes:
    m = [1, 0, 0, d[0] / 1000, 0, 1, 0, d[1] / 1000, 0, 0, 1, d[2] / 1000, 0, 0, 0, 1]
    call("POST", f"/assemblies/d/{D}/w/{W}/e/{ASM}/occurrencetransforms",
         {"occurrences": [{"path": [pid]}], "transform": m, "isRelative": True})
    print(f"  reassentada {n}: {[round(v, 2) for v in d]}")

if correcoes:
    vm = "0.612,0.612,0,0,-0.354,0.354,0.707,0,0.707,-0.707,0.707,0"
    im = call("GET", f"/assemblies/d/{D}/w/{W}/e/{ASM}/shadedviews"
                     f"?viewMatrix={vm}&outputWidth=1400&outputHeight=1000&pixelSize=0&edges=show&useAntiAliasing=true").get("images")
    if im:
        open("out/asm_reassentada.png", "wb").write(base64.b64decode(im[0]))
        print("  out/asm_reassentada.png")
