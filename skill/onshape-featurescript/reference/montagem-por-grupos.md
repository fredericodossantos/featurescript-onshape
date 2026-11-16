# Montagem por grupos rígidos a partir de um STEP (procedimento comprovado no ASTRO, 26/10/2026)

Use quando um STEP importado tem centenas de corpos soltos e o usuário quer uma **aba de montagem
com poucos grupos que se movem juntos** (um grupo por link do robô), para arrastar e medir.
Exemplo real: ASTRO, 364 instâncias, 5 grupos (base, J1, J2, J3, J4). Scripts em
`exemplos-reais/astro-montagem/` quando existirem; senão, no repositório do projeto ASTRO
(`onshape/gerar_grupos_fs.py`, `onshape/montagem_grupos.fs`).

Só use a API com autorização do usuário na mensagem atual (veja `regras-colaborador.md`). Este
procedimento custou **cerca de 200 chamadas**; o grosso foi tentativa e erro, que este texto evita.
Estimativa de um refazer limpo: ~110 chamadas (a maior parte são as inserções de peças, 1 por peça).

## O que a API NÃO faz (não repita a tentativa)

1. **FeatureScript não roda dentro de uma aba de montagem.** Só em Part Studio.
2. **A API não insere peça composta** (`opCreateCompositePart`) na montagem. `POST .../instances` com o
   `partId` do composto responde **200 com corpo vazio e não cria nada** (com `includePartTypes`
   `PARTS` ou `COMPOSITE_PARTS`). Inserir o Part Studio inteiro (`isWholePartStudio: true`) insere os
   **corpos soltos** (398 no ASTRO), nunca o composto. Na interface, o usuário insere o composto com um clique;
   por API, não.
3. **Fundir (UNION) o link num corpo só falha** quando as peças só se tocam por face ou têm folga: o
   corpo não vira um só. Não conte com isso; confira com `/parts` quantos corpos sobraram.
4. **`setProperty` com `PropertyType.NAME` dentro de um loop sobre os corpos do STEP deu ERROR** sem
   mensagem (sem o loop, o feature fecha OK). Não se descobriu a causa. Renomear um corpo isolado por
   `/featurescript` (avaliação) funciona. **Use a cor da peça** para saber o grupo (item abaixo).
5. `microversionId` no corpo do `POST instances` dá 404; `configuration: "default"` dá 200 vazio e
   não cria nada. Use o corpo mínimo do passo 6.
6. A API **não expõe mensagens de aviso/erro** do feature (`featureStates` traz só o status). Para
   depurar, use o endpoint de avaliação (passo "Depuração").
7. Nesta rodada **não se conseguiu** criar mates nem tornar o subconjunto rígido por API (ver "Não resolvido").

## Caminho que funcionou

Dados de entrada: um JSON com a **caixa (xmin..zmax, mm) e o link de cada instância** do STEP (no
ASTRO: `dados/astro_twin.json`, campos `pecas[].caixa_mm.valor`, `.link`, `.simplificada`).

1. **Part Studio novo** (`POST /partstudios/d/{did}/w/{wid}` com `{"name": ...}`).
2. **Copiar o `Import` do STEP**: leia `GET /partstudios/.../features` do Part Studio que já importou,
   tire todo campo `nodeId` do JSON do feature `importForeign` e faça
   `POST /partstudios/.../features` no novo com `{"btType":"BTFeatureDefinitionCall-1406","feature": ...}`.
   O blob do STEP é o mesmo (`BTMParameterReferenceCADImport-2016`, `namespace` com o id do blob). Status OK.
3. **Feature Studio** com um feature sem parâmetros que, para cada corpo sólido:
   - acha a **menor instância cuja caixa contém a caixa do corpo** (tolerância 0,5 mm). Isso resolve as
     peças que o Onshape importa em vários corpos (motores e redutores) sem tabela especial; no ASTRO,
     0 de 364 instâncias caíram no link errado (conferido em Python antes);
   - **apaga as peças pequenas** (`opDeleteBodies`), lista aprovada pelo usuário (no ASTRO, 322 de 364);
   - **pinta cada peça pela cor do seu link** (`setProperty ... PropertyType.APPEARANCE`), uma cor distinta por link.
   Gere o `.fs` com um script Python que embute a tabela de caixas (ver `gerar_grupos_fs.py` do ASTRO). Suba e
   instancie com `scripts/onshape_build_into.py --ps <novo> --reuse --update` (~9 chamadas + 3 de render).
4. **Separar os grupos pela cor**: `GET /parts/d/{did}/w/{wid}/e/{ps}`; cada item traz `partId` e
   `appearance.color`. Agrupe por cor (ASTRO: 76 peças restantes = 29 J1, 2 base, 6 J2, 38 J3, 1 J4).
5. **Uma sub-montagem por grupo**: `POST /assemblies/d/{did}/w/{wid}` com `{"name": "Grupo J1 ..."}` e, para
   cada peça do grupo:
   `POST /assemblies/d/{did}/w/{wid}/e/{asm}/instances` com
   `{"documentId": did, "elementId": <Part Studio>, "includePartTypes": ["PARTS"], "partId": <id>}`
   (resposta 200 vazia é normal; confirme depois com `GET .../e/{asm}` e `len(rootAssembly.instances)`).
6. **Montagem principal**: crie outra assembly e insira as sub-montagens com
   `{"documentId": did, "elementId": <sub-montagem>, "isAssembly": true}`. As peças entram na **pose do STEP**
   (transformação identidade); confira com `.../shadedviews`.
7. **Conferência**: contagem de instâncias por sub-montagem igual à esperada, render de frente da montagem
   principal (olhe a imagem), `featureStatus` do feature no Part Studio.

## Depuração (endpoint de avaliação)

`POST /partstudios/d/{did}/w/{wid}/e/{eid}/featurescript` com
`{"script": "function(context is Context, queries is map) {...}", "queries": {}}` roda FeatureScript
sobre o contexto atual. Use `try { } catch (e) { out.erro = toString(e); }` e leia o resultado no JSON.
Serve para descobrir o que um `setProperty`/`getProperty` faz sem subir feature nenhum. Regex útil para ler:
`re.findall(r'"value" : "([^"]*)"', texto)`.

## Não resolvido (deixar para o usuário na interface, ou investigar com orçamento de chamadas)

- **Rigidez dos subconjuntos**: o JSON da montagem mostrou `isRigid: false` para as sub-montagens. Não se
  achou o endpoint para torná-las rígidas (`POST .../modify` aceitou `{"rigid": true}` com 204, mas sem efeito
  comprovado). Peça ao usuário para conferir arrastando um grupo; se só uma peça mexer, clique direito na sub-montagem >
  tornar rígida.
- **Mates** (fixar a base, revolute/slider entre grupos): a documentação da API mostra `BTMMate-64` com
  `BTMFeatureQueryWithOccurrence-157` por `featureId` de mate connector, e conector de montagem
  `BTMMateConnector-66` com `originType: ON_ENTITY` e `originQuery` (inferência + `path` da instância +
  `deterministicIds` de `getBodyDetails`). Não foi tentado. Mate connectors criados no Part Studio por
  `opMateConnector` com `owner` num corpo composto **não chegam** à montagem por esse caminho (o composto não é
  inserido). Se precisar de mates por API, parta de `BTMMateConnector-66` na própria montagem.

## Armadilhas desta rodada (somar às de `armadilhas-api.md`)

- 4xx não conta na cota, mas **200 com corpo vazio conta e pode não ter feito nada**: sempre releia o alvo.
- `GET /partstudios/.../features` mostra só o status por feature; um status `WARNING` pode ser um aviso do
  próprio script (ex.: o fallback de fusão), não erro.
- Um Part Studio com composto: `/parts` lista composto **e** constituintes; não some os dois.
- Deixar o rascunho para trás (assembly com peças soltas): não apague sem pedido do usuário; avise onde está.
