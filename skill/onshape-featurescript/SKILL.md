---
name: onshape-featurescript
description: Criar, revisar e depurar FeatureScript (.fs) para Onshape e operar a API REST do Onshape (subir Feature Studio, instanciar feature com parametros, medir corpos, renderizar, conferir montagem). Use quando a tarefa envolver arquivos .fs, pecas parametricas no Onshape, Part Studio/Assembly via API, featureStatus/featurespecs, ou regenerar pecas de um projeto CAD do usuario.
---

# Onshape + FeatureScript

Skill reconstruida em 2026-09-30 a partir do que sobrou da maquina antiga
(prompt do agente colaborador, API_NOTES do repo onshape-kitchen-featurescript,
armadilhas do HANDOFF da mesa XY e o .fs da mesa XY que regenera no Onshape).
As skills originais (`onshape-featurescript`, `onshape-featurescript-tutor`)
se perderam com a maquina; `onshape_inspect.py` foi reescrito e ainda nao foi
testado contra a API.

Fonte da skill: repo `C:\dev\featurescript-onshape` (pasta `skill/`), que tambem
guarda FeatureScripts reais que regeneram (`exemplos-reais/`). Mudou algo aqui:
commit la.

Para escrever um .fs novo, parta de `reference/idioma-comprovado.fs` (funcoes
auxiliares comprovadas + feature minimo) e instancie com
`reference/idioma-comprovado.params.json` como modelo de `--params`.

## Antes de escrever codigo

1. Leia `reference/regras-colaborador.md` (regras do usuario - valem sempre).
2. Leia `reference/armadilhas-api.md` (cada item custou um ciclo de debug).
3. Leia `reference/idioma-comprovado.fs` e o .fs do projeto que sera alterado,
   inteiro. Se o projeto tiver HANDOFF/SPEC com cotas, leia a parte relevante.
4. Primeira resposta em ate 10 linhas: geometria entendida, arquivo de
   referencia, estrategia, se ha boolean, como verificar sobreposicao, nome do
   arquivo novo (ou edicao do existente, se o usuario pediu), se usara a API.
   Depois aja - nao fique planejando.

## Regras que mais quebram

- Cota tem origem: MEDIDO, INFORMADO, DERIVADO, ASSUMIDO ou DESCONHECIDO.
  Nunca apresente hipotese como medida.
- Numeros puros em mm no corpo; unidade so na fronteira
  (`definition.x / millimeter` na entrada, `* millimeter` nos vetores).
- `isLength(x, { (millimeter) : [min, DEFAULT, max] } as LengthBoundSpec)` -
  default no meio.
- `&&`/`||`, sem ternario, sem function dentro de function, `var`, ASCII puro.
- Enum de parametro: `export enum` + `annotation` em cada valor.
- UNION: so `tools` (alvo dentro do `qUnion`), sem `targets`. Corpos unidos
  precisam de sobreposicao positiva (>= 0,5 mm); tangencia e FAIL. Ferramenta
  de corte tem que atravessar (margem de 1 mm).
- `fCylinder` existe (`bottomCenter`/`topCenter`/`radius`).
- Erro "non-manifold" sem linha: procurar aresta onde dois cortes se encontram e
  deixam material em quadrantes diagonais; recuar uma peca 0,3 mm
  (armadilha 19).
- Guarda = `throw regenError("... qual parametro ajustar")`; sem `throw` e
  guarda morta.
- Cada cota numa fonte so (funcao `cotas()`); pecas que se encaixam consomem
  o mesmo numero.
- **Sem sujeira: apague o que nao e usado.** Const, function, enum, variavel,
  parametro ou comentario de uma versao anterior (ex.: constantes da correia
  depois que o eixo passou a fuso) sai do arquivo na mesma alteracao que o
  tornou morto - apagar, nao comentar. O Onshape marca como "Unused
  declaration" e o usuario nao quer nenhum warning. Rode
  `python scripts/fs_unused.py arquivo.fs` antes de entregar; tem de dar 0.
  Ele cobre so declaracoes de topo: variaveis locais e parametros sem uso
  confira lendo o diff.

## API do Onshape - politica

- **So com autorizacao explicita do usuario na mensagem atual.** Antes, diga:
  quantas chamadas, leitura ou escrita, qual documento/workspace/elemento, e se
  algo sera criado, alterado ou apagado. Plano free: ~2.500 chamadas/ano
  (4xx nao conta).
- Chaves em `ONSHAPE_ACCESS_KEY` / `ONSHAPE_SECRET_KEY` (variaveis de
  ambiente). Nunca peca as chaves no chat nem as grave em arquivo versionado;
  se faltarem, diga ao usuario para defini-las (Onshape > My account >
  Developer > API keys).
- Trabalhe no documento do usuario (`--did/--wid` do link
  `cad.onshape.com/documents/<did>/w/<wid>/e/<eid>`); nao crie documentos novos
  sem necessidade.
- Depois de escrever: leia de volta, confira `featureStatus`, e **abra o render**.
  HTTP 200 ou `featureStatus=OK` nao provam geometria correta.

## Scripts (`scripts/`, Python 3 + requests)

| Script | Faz | Custo aprox. |
|---|---|---|
| `onshape_build_into.py` | sobe o .fs num Feature Studio do documento existente, sincroniza a versao da std, instancia/atualiza o feature com `--params`, renderiza iso/top/front | ~8-10 chamadas |
| `onshape_inspect.py` | lista elementos e mede cada corpo (bbox, volume, faces planas e cilindricas) no formato `B|C|P` | 1 + 1 por Part Studio |
| `onshape_render.py` | re-renderiza Part Studio ou Assembly em varios angulos | 1 por vista |
| `fs_unused.py` | lista const/function/enum de topo nunca usados (o "Unused declaration" do Onshape); rodar antes de entregar | 0 (local) |

Uso tipico (regenerar pecas preservando a montagem):

```bash
python ~/.claude/skills/onshape-featurescript/scripts/onshape_build_into.py \
  --did <did> --wid <wid> --ps <eid_part_studio> --fs pecas.fs \
  --params params.json --fsname "Nome do Feature Studio" --reuse --update --out out
```

- `--update` preserva os `partId` (montagem continua valida). **Nunca
  `--replace`** quando existe Assembly usando as pecas: as instancias ficam orfas.
- `params.json` = lista de `{"id", "type", "value"[, "enumName"]}`, `type` em
  `length|angle|int|real|bool|enum`; `value` de comprimento com unidade
  (`"8 mm"`). Sem `--params` o feature regenera com `definition.*` indefinido e
  da ERROR.

Medir e conferir:

```bash
python ~/.claude/skills/onshape-featurescript/scripts/onshape_inspect.py \
  --did <did> --wid <wid> [--only "nome do Part Studio"] > medidas.txt
```

Linhas: `B|i|nome|xmin,ymin,zmin|xmax,ymax,zmax|volume_mm3`,
`C|i|raio|eixo|origem|area` (faces cilindricas), `P|i|normal|origem|area`
(faces planas). Scripts de projeto (ex.: `reassentar.py` da mesa XY) leem as
linhas `B`.

## Verificacao antes de dizer "pronto"

A definition declarada/usada - B mapa de cotas fechado - C Ids resolvem -
D tabela de sobreposicao de todos os pares unidos - E auditoria dimensional
com defaults - F scan ASCII - G grep de `throw regenError`, ternarios,
unidades e queries - H `fs_unused.py` = 0 e nada morto no diff. Falhou algum:
escreva `NAO PRONTO` e qual item.
Sem API autorizada: entregue o arquivo e diga que a regeneracao nao foi
verificada.
