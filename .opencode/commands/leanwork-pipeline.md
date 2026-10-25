---
description: Pipeline SDD Leanwork — ponto de entrada (fluxo completo, trilha rápida ou ponto de partida) e leitura de estado para sugerir a próxima ação.
---

# Pipeline SDD Leanwork

Ponto de entrada do pipeline **Spec-Driven Development** da Leanwork (fonte oficial `D:/Projetos/leanwork-sdd-v2`, somente leitura), adaptado ao runtime OpenCode e à operação em `main` deste projeto.

Os detalhes das fases estão nas skills locais de `.opencode/skills/`: `architect-leanwork`, `prd-leanwork`, `prototype-leanwork`, `planner-leanwork`, `reviewer-leanwork` e `context-leanwork`. Skills são invocadas pela skill tool pelo nome.

Demanda informada: $ARGUMENTS

**Contexto deste projeto.**

- Stack: C# / .NET 10, ASP.NET Core Razor Pages (`AGENTS.md` da raiz)
- Catálogo de segurança/desempenho adotado: `D:/Projetos/leanwork-sdd-v2/stacks/dotnet/boas-praticas/`
- Operação em `main`, working tree: sem branches de tarefa, sem commits/merges automáticos (permission `bash` do `opencode.json` bloqueia)
- Gates (`validate_plan`, `validate_review`, `validate_ids`) exigem Python, **ainda não autorizado neste ambiente** — toda verificação de gate é manual e precisa ser anunciada no chat como "gate automático não rodou" (regra de "Degradação silenciosa" do README oficial)

## Passo 1 — Ler o estado

**Maneira preferida** (requer Python, hoje indisponível neste ambiente): rodar `python "D:/Projetos/leanwork-sdd-v2/scripts/status.py" --json` na raiz do projeto. A saída lista artefatos presentes e, por plano, tarefas por status, `Em andamento`, `Bloqueado` com motivo, `Concluído` sem review, review `Bloqueado` sem round seguinte, status fora do vocabulário e a próxima tarefa elegível. É relatório, não gate: interpretar a saída, não reler os artefatos para refazer a contagem.

**Modo degradado (padrão hoje):** ler `docs/plans/`, `docs/reviews/` e os demais artefatos à mão, aplicando as mesmas categorias da saída do script, e **dizer no chat que o estado foi reconstruído sem o script** (gate automático não rodou). Sem branches de tarefa neste runtime, a checagem extra é `git status --porcelain` para trabalho em curso na working tree.

## Passo 2 — Entrada ou continuidade

**Sem artefatos do pipeline, ou com demanda nova em `$ARGUMENTS`:** conduzir a entrada, perguntando **uma única vez**:

> Essa demanda é:
>
> **A.** Projeto/sistema novo — começar pela arquitetura (`architect-leanwork`)
> **B.** Feature nova num sistema cuja arquitetura já está definida — começar pelo PRD (`prd-leanwork`)
> **C.** Feature já documentada em PRD, falta o plano — ir direto ao `planner-leanwork`
> **D.** Mudança pequena e reversível (poucos arquivos, sem migration, contrato público, schema ou permissão) — trilha rápida, sem PRD
> **E.** Não sei ainda, me ajuda a decidir

- **A**: usar a skill `architect-leanwork`; ao concluir, perguntar se encadeia para o PRD
- **B**: perguntar se há proposta arquitetural para ler; em seguida, `prd-leanwork`
- **C**: perguntar o caminho do PRD, lê-lo e invocar `planner-leanwork`
- **D**: usar `planner-leanwork`, seção "Trilha rápida", confirmar o critério de entrada e gerar o plano a partir de `D:/Projetos/leanwork-sdd-v2/skills/planner-leanwork/references/plan-template-quick.md`. Fora do critério, dizer por quê e propor B ou C
- **E**: 2-3 perguntas curtas (existe código? ADR? PRD? a mudança é pequena e reversível?) e propor o ponto de entrada

Estrutura de pastas: `D:/Projetos/leanwork-sdd-v2/templates/folder-conventions.md`. Sugerir, não impor, se o projeto já tem outra.

**Com artefatos:** sugerir **uma** próxima ação, na ordem de prioridade abaixo. Não listar opções.

1. **Resolver bloqueios** — review `Bloqueado` sem round seguinte (citar `R-XX` com o nome do relatório: a numeração recomeça a cada arquivo); tarefa `Bloqueado`, a partir do motivo em `Bloqueio:`. A correção é feita na working tree e fecha com `/leanwork-review T-XX`
2. **Validar entregas** — tarefa `Concluído` sem review: sugerir `/leanwork-review T-XX`
3. **Avançar execução** — tarefa `Em andamento` primeiro (trabalho interrompido na working tree; `/leanwork-execute T-XX` retoma); depois a próxima elegível: `/leanwork-execute T-XX`, que implementa, revisa e fecha
4. **Avançar o pipeline** — PRD com interface sem SPEC-UI e sem plano: oferecer `prototype-leanwork` ou ir direto ao `planner-leanwork`; PRD sem plano: `planner-leanwork`; arquitetura sem PRD: `prd-leanwork`

Antes da sugestão, apontar inconsistências marcadas pelo Passo 1: tarefa `Concluído` com review `Bloqueado` em aberto (grave), `Bloqueado` sem motivo, status fora do vocabulário.

**Sugestão paralela — contexto do agente**, em uma linha junto da principal, nunca no lugar dela, e sem repetir se o usuário ignorou: `AGENTS.md` da raiz ausente ou com `<!-- TODO -->`, módulo novo sem contexto, padrão emergente apontado em review, permissões do `opencode.json` desatualizadas → invocar a skill `context-leanwork` com o escopo (`raiz`, `modulo <nome>`, `permissoes`, `auditar`).

## Regras

- Na continuidade, este comando **sugere**: nenhuma skill é invocada sem confirmação
- Na entrada, cada fase produz um artefato revisável antes da próxima: ao concluir uma fase, parar, mostrar o resultado e perguntar se segue
- Não cobrar SPEC-UI de PRD sem interface, nem pular review de tarefa concluída
- Não gerar nem editar `AGENTS.md` por conta própria
- Nada de commits, branches ou merges: todas as gravações ficam na working tree, e estado versionado é decisão do usuário
