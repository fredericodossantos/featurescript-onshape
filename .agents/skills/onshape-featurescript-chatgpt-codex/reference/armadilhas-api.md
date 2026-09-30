# Armadilhas ja pagas (API REST e FeatureScript)

Fontes: `docs/API_NOTES.md` do repo onshape-kitchen-featurescript (2026-06) e a
secao 7 do `cad/HANDOFF_mesa_xy.md` do projeto cameraobscura (2026-09). Todas
validadas contra a API real.

## API REST

1. **Header `Accept` = `application/json` puro.** A variante
   `application/json;charset=UTF-8; qs=0.09` faz varias rotas devolverem 404 em
   documento que existe (custou 5 chamadas e uma conclusao errada).
2. **Parametros do feature sempre explicitos.** `parameters: []` faz o feature
   regenerar com `definition.*` undefined -> `featureStatus=ERROR` sem mensagem.
   O default da precondition so vale na interface.
3. **`BTMParameterEnum-145` exige `namespace`** = `e{eidFeatureStudio}::m{microversion}`
   (mesmo namespace do feature) e `enumName`. Sem isso: HTTP 400 "Parameter ...
   does not match its feature spec". Vale para enums dentro de arrays.
4. **Array parameter:** `BTMParameterArray-2025` com
   `items: [{"btType": "BTMArrayParameterItem-1843", "parameters": [...]}]`; cada
   item leva TODOS os sub-parametros da precondition.
5. **Diagnostico de compilacao:**
   `GET /featurestudios/d/{did}/w/{wid}/e/{eid}/featurespecs` -> `featureSpecs: []`
   significa que o modulo nao compilou. O endpoint tambem mostra o formato
   esperado de cada parametro (enumName, namespace). Para achar a construcao
   culpada sem a mensagem do editor: subir variantes reduzidas, cada uma no seu
   Feature Studio, e ver quais compilam (bisseccao).
6. `POST /partstudios/.../featurescript` (avaliar expressao) quer `queries` como
   **map `{}`**, nao array.
7. **Atualizar com `--update`, nunca `--replace`.** Apagar e recriar o feature
   muda os `partId`; as instancias da Assembly ficam orfas (render vazio,
   status continua OK).
8. **Inserir "Part Studio inteiro" na montagem vira instancias individuais.**
   Corpo novo no Part Studio nao aparece sozinho na Assembly; e preciso inserir.
9. **Conferir a montagem REAL:** ler as matrizes de `occurrences` da Assembly e
   comparar com o projeto, nao so os numeros que se pretendia. Foi assim que
   passou uma colisao que o usuario viu na tela.
10. A versao da std lib (`FeatureScript N;`) tem de bater com a do servidor:
    ler o `contents` de um Feature Studio recem-criado e reescrever o cabecalho e
    os `version : "N.0"` (o `onshape_build_into.py` faz isso).
11. Cota do plano free: ~2.500 chamadas/ano; resposta 4xx nao conta.

## FeatureScript

12. Enum como tipo de parametro: `export enum X { annotation { "Name" : "A" } A, ... }`.
    `enum X { A, B }` nao compila como parametro (featurespecs vazio).
13. `{ (millimeter) : [min, def, max] } as LengthBoundSpec` e valido (nao precisa
    de `(meter)`).
14. Chave opcional de map: `m["k"] != undefined` (`has()` nao existe).
15. `fCylinder` existe (`bottomCenter`, `topCenter`, `radius`); nao montar
    cilindro com sketch + opExtrude.
16. `opBoolean` UNION nao leva `targets` - so `tools` com o alvo dentro do
    `qUnion`. Com `targets` da "did not regenerate properly" sem apontar linha.
17. Corte tangente e geometria degenerada: faca a ferramenta estourar a parede
    (margem de ~1 mm) ou recuar de proposito.
18. **Layout do Part Studio com vagas fixas** (`const VAGA`). Cursor corrido
    empurra as pecas seguintes quando entra peca nova e a montagem passa a
    apontar para o lugar errado (ja saiu 90 mm fora).

## Eixos (convencao usada nos projetos)

Mundo Onshape Z-up: X = largura, Y = profundidade, Z = altura. Trabalhar em mm
no .fs. Peca `{dims, pos=centro}` vira
`fCuboid(corner1 = pos - dims/2, corner2 = pos + dims/2)`.

## Vistas (viewMatrix de `shadedviews`, 3x4 row-major, Z-up)

- iso: `0.612,0.612,0,0,-0.354,0.354,0.707,0,0.707,-0.707,0.707,0`
- top: `1,0,0,0,0,1,0,0,0,0,1,0`
- front: `1,0,0,0,0,0,1,0,0,-1,0,0`
- right: `0,1,0,0,0,0,1,0,1,0,0,0`
