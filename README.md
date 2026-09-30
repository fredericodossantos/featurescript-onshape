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

## Versão ChatGPT/Codex

A versão separada para ChatGPT/Codex fica em `.agents/skills/onshape-featurescript-chatgpt-codex/`. A skill Claude Code acima e `exemplos-reais/` permanecem nos caminhos originais. A pasta OpenAI inclui referências, scripts e uma cópia dos exemplos reais para formar um pacote autossuficiente; mudanças futuras nos exemplos devem ser sincronizadas entre as duas pastas. O estado de verificação dessa cópia está em [estado-verificacao.md](.agents/skills/onshape-featurescript-chatgpt-codex/reference/estado-verificacao.md).

No Codex, abra este repositório e invoque `$onshape-featurescript-chatgpt-codex` ou `/skills`; skills em `.agents/skills` são descobertas pelo Codex. No ChatGPT Desktop, quando Skills e upload estiverem disponíveis para a conta/workspace, compacte a pasta da skill, faça o upload em **Skills > Create > Upload from your computer** e invoque `@onshape-featurescript-chatgpt-codex`. Veja [Build skills - OpenAI](https://learn.chatgpt.com/docs/build-skills) e [Skills in ChatGPT - OpenAI Help Center](https://help.openai.com/en/articles/20001066-skills-in-chatgpt).

Para gerar o ZIP no PowerShell, a partir da raiz do repositório:

```powershell
Compress-Archive -Path .agents\skills\onshape-featurescript-chatgpt-codex -DestinationPath "$env:TEMP\onshape-featurescript-chatgpt-codex.zip" -Force
```

O upload não configura credenciais Onshape nem garante acesso a Python ou aos arquivos locais. Scripts que chamam a API ainda precisam de Python 3, `requests`, credenciais no ambiente de execução e autorização explícita do usuário conforme as regras da skill.
