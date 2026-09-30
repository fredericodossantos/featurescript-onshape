# Estado de verificação registrado no repositório

Use esta evidência ao escolher ou citar um exemplo. "Usado/regenera OK" aplica-se ao arquivo original e na data indicada; não valida automaticamente cópias, exemplos derivados ou o estado atual do Onshape.

| Item | O que o repositorio afirma | O que isso nao comprova |
|---|---|---|
| `scripts/onshape_build_into.py` | Cópia da versão usada na mesa XY; funcionava em 2026-09-21. | Não comprova execução atual nem que toda combinação de parâmetros funcione. |
| Funções auxiliares em `reference/idioma-comprovado.fs` | Copiadas de uma versão de `examples/mesa-xy/mesa_xy_pecas_fuso.fs` que regenera OK no Onshape em 2026-09-21. | Não comprova identidade com a cópia local de 2026-09-23 nem que o feature `Exemplo - bloco com furo` da referência foi regenerado; esse feature **ainda não foi regenerado**. |
| `scripts/onshape_render.py` | Generalizado a partir do script antigo. | Ainda não foi executado. |
| `scripts/onshape_inspect.py` | Reescrito em 2026-09-30 depois que o original se perdeu; mantém o formato B/C/P consumido por `examples/mesa-xy/reassentar.py`. | **Não foi testado contra a API.** O consumo do formato pelo script de reassentamento não valida a nova implementação. |
| `examples/mesa-xy/mesa_xy_pecas.fs` | Cópia local de 2026-09-23; o README original diz que ainda não foi comparada com o Onshape e que há warnings pendentes. | Não afirmar que esta cópia local é a versão atual no Onshape ou que está sem warnings. |
| `examples/mesa-xy/mesa_xy_pecas_fuso.fs` | A cópia local inclui a alteração do commit `f61244b` (2026-09-30), que adiciona canais no suporte do motor; o README do repositório não registra uma regeneração Onshape dessa alteração. | A regeneração em 2026-09-21 refere-se à versão usada naquela data e não comprova a alteração de 2026-09-30. Não apresentar a versão nova como regenerada/confirmada. |
| `examples/mesa-xy/saca_rolamento_linear_lm8uu.fs` | Cópia local de 2026-09-23; o README original inclui todos os `.fs` entre os não comparados com o Onshape e com warnings pendentes. O comentário do arquivo também diz que dimensões foram validadas contra um STL. | O repositório não declara que este FeatureScript foi regenerado no Onshape. Validação contra STL não é regeneração. |
