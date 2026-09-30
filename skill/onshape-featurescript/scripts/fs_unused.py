#!/usr/bin/env python3
"""Lista declaracoes de topo nao usadas num .fs (o que o Onshape marca como
"Unused declaration"): const, function e enum declarados e nunca referenciados.

    python fs_unused.py arquivo.fs [outro.fs ...]

Sai com codigo 1 se achar algo. Roda local, sem API. Rodar SEMPRE antes de
entregar um .fs: declaracao morta e sujeira e deve ser apagada, nao comentada.

Limites: olha so declaracoes de topo (nao variaveis locais), ignora comentarios
e strings, e trata como usado qualquer nome exportado ("export const/function/enum")
porque pode ser consumido por outro Feature Studio.
"""
import re
import sys


def strip_comments_and_strings(src):
    src = re.sub(r"/\*.*?\*/", " ", src, flags=re.S)
    src = re.sub(r"//[^\n]*", " ", src)
    return re.sub(r'"(?:\\.|[^"\\])*"', '""', src)


def unused(path):
    raw = open(path, encoding="utf-8").read()
    code = strip_comments_and_strings(raw)
    decl = re.compile(r"^(export\s+)?(const|function|enum)\s+([A-Za-z_]\w*)", re.M)
    found = []
    for m in decl.finditer(code):
        if m.group(1):
            continue
        name = m.group(3)
        if len(re.findall(r"\b%s\b" % re.escape(name), code)) == 1:
            line = code.count("\n", 0, m.start()) + 1
            found.append((line, m.group(2), name))
    return found


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    bad = 0
    for p in sys.argv[1:]:
        items = unused(p)
        print(f"== {p}: {len(items)} nao usada(s)")
        for line, kind, name in items:
            print(f"  {line}: {kind} {name}")
        bad += len(items)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
