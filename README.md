# FeatureScript Onshape

Cópia de segurança e fonte única da skill do Claude Code para FeatureScript e para a
API REST do Onshape, junto com FeatureScripts reais que regeneram no Onshape.
Nasceu em 2026-09-30, depois que a máquina antiga se perdeu com as skills originais.

## Estrutura

| Caminho | O que é |
|---|---|
| `skill/onshape-featurescript/` | a skill (SKILL.md, referências, scripts). É a pasta que o Claude Code carrega. |
| `skill/.../reference/regras-colaborador.md` | regras de trabalho para agentes que escrevem .fs |
| `skill/.../reference/armadilhas-api.md` | armadilhas já pagas da API e do FeatureScript |
| `skill/.../reference/idioma-comprovado.fs` | **modelo base** para agentes: funções auxiliares comprovadas + feature mínimo |
| `skill/.../reference/idioma-comprovado.params.json` | parâmetros para instanciar o modelo via API |
| `skill/.../scripts/` | `onshape_build_into.py` (subir/instanciar/renderizar), `onshape_inspect.py` (medir), `onshape_render.py` (renderizar) |
| `exemplos-reais/mesa-xy/` | FeatureScripts da mesa XY (projeto cameraobscura), parâmetros e `reassentar.py` |

## Instalar a skill numa máquina nova

A skill global fica em `~/.claude/skills/onshape-featurescript`. Neste PC ela é uma
junção (junction) apontando para a pasta do repositório, então editar aqui já vale
para o Claude:

```powershell
cmd /c mklink /J "$env:USERPROFILE\.claude\skills\onshape-featurescript" "C:\dev\featurescript-onshape\skill\onshape-featurescript"
```

(Sem junção, copiar a pasta também funciona.)

Dependência dos scripts: Python 3 com `requests` (`pip install requests`).

## Chaves da API

Onshape → My account → Developer → API keys. Defina no Windows, como variáveis de
ambiente do usuário, `ONSHAPE_ACCESS_KEY` e `ONSHAPE_SECRET_KEY`, e reinicie o
terminal/app. **Nunca** grave as chaves neste repositório (o `.gitignore` já
exclui `.env`).

## Estado da verificação

| Item | Estado |
|---|---|
| `onshape_build_into.py` | cópia da versão usada na mesa XY (funcionava em 2026-09-21) |
| `onshape_render.py` | generalizado a partir do script antigo; não rodado ainda |
| `onshape_inspect.py` | **reescrito sem teste** (o original se perdeu). Conferir uma peça conhecida no primeiro uso |
| `idioma-comprovado.fs` | funções auxiliares vêm do .fs da mesa XY (regenera OK); o feature de exemplo **ainda não foi regenerado** no Onshape |
| `exemplos-reais/mesa-xy/*.fs` | cópias locais de 2026-09-23; ainda não comparadas com o que está no Onshape; warnings pendentes |

Documento Onshape da mesa XY:
<https://cad.onshape.com/documents/ef2faa3446bb2591886d7fa9/w/e99d15a7af85fd99690a70fc/e/9304b9ba04e2ea59cc7bb5e9>
