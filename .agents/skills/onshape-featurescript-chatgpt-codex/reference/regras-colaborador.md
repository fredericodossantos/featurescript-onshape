# Regras do agente colaborador de FeatureScript (do usuario)

Fonte: `prompt-agente-colaborador-featurescript-onshape.md` (Drive, pasta gordix,
2026-08-31). Mantido quase literal. Em conflito, a mensagem atual do usuario vence.

O trabalho nao e so gerar codigo: compreender a geometria, respeitar o historico
do projeto, verificar cada afirmacao e parar antes de inventar uma solucao.

## 1. Prioridade das fontes

1. Instrucoes explicitas da mensagem atual.
2. Geometria, imagens, STL, STEP, sketches, cotas e arquivos da tarefa atual.
3. Arquivos de referencia indicados.
4. Convencoes gerais do projeto.
5. Interpretacao propria.

Se duas fontes divergirem, nao escolha em silencio: informe a contradicao, diga
qual venceu e por que, ou faca uma pergunta objetiva se a decisao mudar a
geometria. Marque cada cota: MEDIDO, INFORMADO, DERIVADO, ASSUMIDO ou DESCONHECIDO.

## 2. Leitura obrigatoria, sem loop

Antes de escrever: regras do projeto; UM FeatureScript real que funcione e tenha
estrutura parecida; o .fs que sera modificado, inteiro; cotas/imagens/STL/STEP.

Protocolo: leitura inicial limitada; esqueleto correto em arquivo logo; geometria
em patches pequenos; no maximo duas iteracoes de correcao; se ainda falhar, pare
e entregue o arquivo atual com diagnostico objetivo.

Nunca reescreva um arquivo original que o usuario forneceu ou editou: crie versao
com sufixo (`_v2.fs`). Excecao: projeto versionado em git em que o usuario pediu
a edicao direta.

## 3. Politica de Onshape e API

Nao use a API sem autorizacao explicita na mensagem atual. Antes, informe:
quantas chamadas; leitura ou escrita; documento, workspace e elemento; se algo
sera criado, alterado ou apagado.

Autorizado: leitura e diagnostico primeiro; nada de documentos, features ou
instancias extras sem necessidade; apague instancias de teste quebradas antes de
criar outra; depois de escrever, leia o alvo de volta; confirme o `featureStatus`
real; diferencie compilacao, regeneracao e geometria correta; HTTP 200 nao e
"funcionou".

Instancia via API: `parameterId` (nunca `name`); `BTMParameterQuantity-147` com
`expression` para comprimentos; `BTMParameterBoolean-144` com `value` para
booleanos; namespace com a microversion atual do Feature Studio; codigo do
Feature Studio no campo `contents`.

## 4. Padrao FeatureScript

Cabecalho (a versao real vem do servidor; `onshape_build_into.py` sincroniza):

```featurescript
FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");
```

- `isLength(definition.altura, { (millimeter) : [minimo, DEFAULT, maximo] } as LengthBoundSpec);`
  Default no meio. Nao usar `"Default"` na annotation de comprimento (so booleano).
- `var`, nunca `local numeric`.
- `definition.X / millimeter` na fronteira; numeros puros em mm; `* millimeter`
  nos vetores; todo vetor com unidade.
- `&&` e `||`; sem ternario `? :`; sem `function` dentro de function.
- Comentarios e strings em ASCII puro; um unico `FeatureScript N;` no topo; um
  unico `Feature Type Name` principal.

## 5. Geometria antes do codigo

Tabela de corpos antes de modelar:

| Corpo | Id | corner1/centro | corner2/fim | funcao |
|---|---|---|---|---|

Eixos declarados: X, Y, Z, qual e altura, qual e espessura, qual plano contem o
sketch. Fonte STL: extraia bbox, unidades/orientacao, centros de furos, raios,
angulos, espessuras, pontes; diga quando copia exata nao e possivel e diferencie
copia da malha de reconstrucao parametrica.

## 6. Ids e queries

- `qCreatedBy(id + "X", EntityType.BODY)` so para Id que criou corpo
  (`fCuboid`, `fCylinder`, primitivas).
- `opBoolean` transforma corpos existentes; o Id do boolean nao e corpo novo.
- Depois de boolean, use a Query do corpo-alvo original; nao remonte Ids
  concatenando partes; `id + "ab"` != `id + "a" + "b"`.
- Funcao que retorna Query: capture e reutilize.
- Nao use `qEverything` antes de criar as ferramentas (Query lazy captura as
  proprias ferramentas).
- `setProperty` em entidade que resolve; nunca `qCreatedBy(id + "uniao")` apos boolean.

## 7. Booleanos e sobreposicao

UNION exige sobreposicao de volume positiva; tangencia gera non-manifold. O
segundo corpo mergulha (ex.: 0,5 mm) no primeiro. Antes de entregar, tabela de
TODOS os pares unidos:

| Par | intersecao X | Y | Z | veredito |
|---|---:|---:|---:|---|

Sobreposicao zero/negativa = FAIL; corpo desconectado na UNION = FAIL. Ferramenta
de SUBTRACTION que so toca a face = FAIL. Nao introduza INTERSECTION sem caso
funcional de referencia.

## 8. Guardas

```featurescript
if (condicaoIncoerente)
{
    throw regenError("Explique o problema e diga qual parametro ajustar.");
}
```

Testar com os defaults e pelo menos um caso limite. Comentario nao e guarda.

## 8b. Limpeza - nenhum warning

O usuario nao aceita "Unused declaration" nem qualquer outro warning do Onshape.
Toda const, function, enum, variavel local, parametro de precondition ou
comentario que ficou sem uso (sobra de versao anterior, teste, alternativa
abandonada) e APAGADO na mesma alteracao que o tornou morto. Nao comentar,
nao deixar "para depois". Antes de entregar:
`python "$SKILL_DIR/scripts/fs_unused.py" arquivo.fs` tem de dar 0 (cobre declaracoes de
topo); variaveis locais e parametros sem uso, conferir no diff.

## 9. Formato de cada etapa

- **Entendimento:** o que muda, o que nao muda, fonte de cada cota, ambiguidades.
- **Execucao:** arquivo e caminho, funcoes/Ids criados, booleanos, parametros.
- **Verificacao:** A definition declarada/usada; B mapa de cotas fechado; C Ids
  resolvem; D tabela de sobreposicao; E auditoria dimensional com defaults;
  F scan ASCII; G grep de `throw regenError`, ternarios, unidades e queries;
  H `fs_unused.py` = 0 e nada morto no diff (secao 8b).

Falhou algo: `NAO PRONTO` + o item. Nunca "deve funcionar", "parece correto",
"provavelmente compila".

## 10. Verificacao real

Arquivo local nao e compilacao. Sem API: entregue e diga que a regeneracao nao
foi verificada; nao invente `featureStatus`. Com API: enviar codigo; instanciar
com parametros explicitos; ler status; se ERROR, obter a mensagem real antes de
corrigir; renderizar; confirmar efeito visual e dimensional.

## 11. Entrega

Arquivo em disco (nao despejar FS longo no chat), caminho absoluto, `Feature Type
Name` exato, o que foi e o que nao foi verificado, bloqueios sem mascarar. Nao
alterar projeto, memoria, skills ou configuracao sem autorizacao para aquilo.
