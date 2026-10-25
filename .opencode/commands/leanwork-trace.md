---
description: Gera a matriz de rastreabilidade ADR ↔ RN ↔ CA ↔ UI ↔ T ↔ R a partir dos artefatos do pipeline SDD. Relatório, não gate.
---

# Matriz de rastreabilidade SDD

Conectar arquitetura (ADRs) → regras (RNs) → cenários (CAs) → telas (UIs, quando houver) → tarefas (Ts) → reviews (Rs), e apontar onde a cadeia quebra.

Artefato alvo: $ARGUMENTS

## Passo 1 — Rodar o script

**Maneira preferida** (requer Python, hoje negado neste ambiente): na raiz do projeto, com `--prd docs/prds/PRD-XXX-nome.md` quando o usuário indicar um PRD:

```bash
python "D:/Projetos/leanwork-sdd-v2/scripts/trace.py" --mermaid
```

A saída traz a matriz (uma linha por `CA-XX`, com RN, ADR, UI, T, último review e, com git, os commits cujo trailer `Refs:` cita a tarefa) e as **lacunas mecânicas**: RN sem cenário, CA sem tarefa, tarefa sem `Implementa` nem `Valida`, ADR citado e inexistente, ADR nunca citado, `Concluído` sem review, review `Bloqueado` sem round seguinte, status fora do vocabulário, `Bloqueado` sem motivo e, com SPEC-UI, tela e estado sem tarefa. Sem SPEC-UI, as colunas de UI somem e isso não é lacuna. `--json` dá a mesma coisa estruturada.

Nesta configuração (operação em `main`, sem commits do pipeline) a coluna de commits tende a sair vazia com aviso: não é lacuna, é a divergência declarada no `AGENTS.md` (seção Integração SDD). Se o usuário commitou manualmente com trailer `Refs:`, a coluna volta.

**Modo degradado (padrão hoje):** montar a matriz lendo PRD, plano, SPEC-UI e reviews com as mesmas categorias, e **dizer no chat que a matriz não saiu do script** (gate automático não rodou). Não refazer a matriz por colunas de outro jeito: o script é a fonte quando existe.

## Passo 2 — Interpretar as lacunas

Para cada lacuna, dizer o risco e uma ação, sem inventar o elo que falta:

- **`Concluído` com review `Bloqueado` em aberto** é a mais grave — estado declarado e estado validado divergem. Vem primeiro
- **Tarefa sem rastro** é legítima quando estrutural (fundação, migration); investigar antes de reportar como problema
- **ADR nunca citado** pode ser decisão sem impacto ou over-engineering — perguntar, não concluir

O script não julga o que pede leitura de conteúdo. Com SPEC-UI, verificar à parte:

- **CA de interface sem tela** — cenário com ator em tela que nenhuma `UI-XX` cobre
- **Tela sem respaldo no PRD** — `UI-XX` que não mapeia para `RN-XX` nem `CA-XX`: escopo extra ou lacuna do PRD
- **Lacunas abertas da SPEC-UI** — itens da seção 8 do documento sem decisão registrada

`R-XX` é sempre citado com o nome do relatório (`R-01 (REVIEW-T-04-2026-06-15)`): a numeração recomeça a cada arquivo.

## Passo 3 — Salvar (opcional)

Perguntar se o usuário quer salvar a matriz como `docs/traceability/MATRIX-{nome_do_prd}.md`, para versionar junto dos outros artefatos (o git só entra se o usuário commitar).

## Regra de ouro

Análise estática dos artefatos: não inventar link que não está escrito. Elo ausente é lacuna, não adivinhação.
