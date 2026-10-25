---
description: Executa uma tarefa do plano SDD na working tree de main, carregando o contexto declarado (RN/CA/ADR/UI), e encadeia a review num sub-agente de contexto limpo. Sem commits, branches ou merges automáticos.
---

# Executar tarefa do plano SDD

Tarefa alvo: $ARGUMENTS

Adaptação do `/leanwork-execute` oficial (`D:/Projetos/leanwork-sdd-v2/commands/leanwork-execute.md`) para este runtime, com duas divergências declaradas deste projeto:

- **Sem branch de tarefa.** A implementação acontece direto na working tree de `main`. O diff da review é `git diff HEAD` mais os arquivos não rastreados tocados. O custo declarado: o diff da tarefa se mistura com qualquer outra mudança não commitada que apareça na árvore — por isso este exige árvore limpa antes de começar, mais que o original.
- **Sem commits automáticos.** A permissão `bash` do `opencode.json` nega `git add/commit/merge/push`. O trailer `Refs:` e o gate `check_commit.py` não se aplicam nesta configuração; o `Status:` no plano é a única trilha de estado, e o versionamento é decisão do usuário (feita por ele, fora do comando).

Este comando executa **uma** tarefa do plano, de ponta a ponta: carrega o contexto declarado, implementa neste contexto, atualiza o plano e entrega a review a um sub-agente de contexto limpo. Ele existe para que a implementação carregue o mesmo contexto que o planejamento produziu — em vez de o agente reconstruir a intenção a partir do título da tarefa.

## O que fazer

### Passo 1 — Localizar o plano e a tarefa

1. Procurar o plano em `docs/plans/PLAN-*.md`. Se houver mais de um, perguntar qual — não escolher pelo mais recente.
2. Se `$ARGUMENTS` traz uma `T-XX`, usar essa tarefa. Se ela não estiver `Pendente`, avisar o estado atual e confirmar antes de seguir.
3. Se `$ARGUMENTS` está vazio, encontrar a primeira tarefa com `Status: Pendente` cujas dependências em `Depende de:` estejam **todas** com `Status: Concluído`. Se nenhuma for elegível, reportar o que está bloqueando e parar.

O vocabulário de status é fixo (`Pendente` / `Em andamento` / `Concluído` / `Bloqueado`) e está em `D:/Projetos/leanwork-sdd-v2/templates/id-conventions.md`. Ler o campo `**Status:**` de dentro do bloco `#### T-XX` — o `Status:` do cabeçalho do plano é status de documento, não de tarefa.

### Passo 2 — Carregar o contexto declarado pela tarefa

Abrir e ler na íntegra o que a tarefa referencia. **Não deduzir pelo título:**

- `Implementa:` → o PRD, cada `RN-XX` listado
- `Valida:` → o PRD, cada `Cenário [CA-XX]:` listado. No plano de trilha rápida (`PRD de referência: nenhum — trilha rápida`) não há PRD: os cenários estão no bloco `**Cenários:**` da própria tarefa
- `Decisões base:` → a proposta arquitetural, cada `ADR-XX`
- `Telas:` → a SPEC-UI, a tela e **cada estado** listado (campo ausente em projeto sem interface — não é lacuna)

Antes de ler, o gate de rastreabilidade: com Python autorizado, `python "D:/Projetos/leanwork-sdd-v2/scripts/validate_ids.py" docs/plans/PLAN-XXX-nome.md`. Python está **negado** neste ambiente (`opencode.json`) — combinado com o usuário, rodar manualmente e mudar a permissão; sem isso, conferir os IDs à mão e **avisar no chat que a rastreabilidade não passou pelo gate automático** (regra de "Degradação silenciosa" do README oficial, `D:/Projetos/leanwork-sdd-v2/README.md`). Cada `ERROR` é um ID citado que não existe na origem — reportar o gap e aguardar, sem inventar o ID. Verificação manual exige o mesmo rigor: identificar cada RN/CA/ADR/UI citado e confirmar a existência no artefato de origem.

Ler também o `AGENTS.md` da raiz (`CLAUDE.md` no original) — é onde estão as convenções que o review vai cobrar.

Se o `AGENTS.md` tem a linha `Catálogo:` em `Segurança e desempenho`, o catálogo é critério da review. Ler `D:/Projetos/leanwork-sdd-v2/stacks/dotnet/boas-praticas/README.md` e os arquivos das áreas que a tarefa toca (endpoint, acesso a dados, chamada HTTP de saída, código assíncrono), pulando os códigos listados em `exceto`. Não ler o catálogo inteiro por padrão: tarefa que só mexe em consulta EF não precisa do arquivo de HttpClient.

### Passo 3 — Verificar pontos de validação humana

Conferir se a tarefa aparece na seção "Pontos de validação humana" do plano. Se aparecer, pedir confirmação explícita **antes de escrever qualquer código** e aguardar resposta.

### Passo 4 — Confirmar a árvore e marcar início

Sem branch de tarefa neste runtime:

1. **Árvore razoavelmente limpa.** `git status --porcelain` — arquivos rastreados modificados precisam da origem explicada: perguntar ao usuário se pertencem a outra mudança em curso. Precedente: o diff da review lê a árvore inteira, e mudança estranha entra na conta desta tarefa
2. **Confirmar a branch.** `git branch --show-current` deve ser `main`. Se outra, parar e avisar o usuário (mudar de branch é decisão dele — a permissão também nega `git switch`)
3. Marcar o `**Status:**` da tarefa no plano como `Em andamento`. Sem commits, a recuperação de sessão vem apenas do plano — não deixar a marca para depois

### Passo 5 — Implementar

Implementar o comportamento que a tarefa promete — os `CA-XX` de `Valida:` e os critérios de aceite. Onde o código mora segue as convenções do repositório e não precisa de justificativa. O que precisa é `**Portas de mão única:**`: tocar migration, contrato público, schema ou configuração de permissão que a tarefa não declara exige parar e confirmar com o usuário antes — porta de mão única não declarada é finding no review, e o reviewer vai encontrar.

Respeitar as decisões de `Decisões base:` mesmo quando houver abordagem que pareça melhor. Se a decisão parecer errada, é caso de ADR novo via `architect-leanwork`, não de divergir na implementação.

### Passo 6 — Escrever e rodar os testes

Escrever os testes de `Testes a escrever:` **antes** do código de produção da costura correspondente, quando praticável — ver o teste falhar primeiro é o que confirma que ele testa a coisa certa, não um teste que passaria mesmo vazio. Nomear cada um conforme `D:/Projetos/leanwork-sdd-v2/templates/id-conventions.md` — `CA_XX_descricao` para os que provam um cenário. É esse nome que fecha o elo `CA-XX → teste` para o `/leanwork-trace`.

Rodar o build do projeto antes de rodar a suíte (`dotnet build DevNotes.slnx`) — erro de tipo não é falha de teste, é implementação incompleta, e roda mais rápido. Rodar os testes (comando definido no `AGENTS.md`; hoje, sem projeto de testes, é lacuna declarada no `AGENTS.md` em `<!-- leanwork-context:build:start -->`). **Não marcar nada como concluído com teste vermelho ou build quebrado.**

### Passo 7 — Fechar o estado

Se os critérios de aceite estão todos atendidos e os testes passam:

1. Conferir cada item de `Critério de aceite (testável)` contra a implementação e a Definition of Done do `AGENTS.md`, quando houver
2. Atualizar o campo `**Status:**` para `Concluído`
3. **Nada de commit.** A permissão `bash` de `opencode.json` nega `git add/commit`. Antes, orientar no chat o que o usuário pode querer commitar (código, testes, plano) com a mensagem sugerida e o trailer `Refs:` — a `T-XX` e os `CA-XX` de `Valida:` (convenção em `D:/Projetos/leanwork-sdd-v2/templates/id-conventions.md`, seção "Trailer de commit"). O commit é ação do usuário, e o gate `check_commit.py` do original não se aplica aqui — lacuna declarada

Se **não** for possível concluir, usar `Status: Bloqueado`, registrar o motivo no campo `**Bloqueio:**` da tarefa e parar — sem review. Tarefa parcialmente feita marcada como `Concluído` é a inconsistência que o `/leanwork-trace` foi feito para achar — não criar uma de propósito.

### Passo 8 — Encadear a review

Com a tarefa `Concluído` no plano, seguir direto para a review: invocar `/leanwork-review <T-XX>`, com os Passos 1 a 7 daquele comando. Lá estão o sub-agente verificador (`task` tool com o agente `reviewer-leanwork`, de `.opencode/agent/reviewer-leanwork.md`), o payload, o relatório, o veredicto e o fechamento — este comando não repete nada disso.

Quem implementou não decide se a implementação passou: o verificador é um contexto que não viu esta conversa. Duas regras seguram essa separação daqui:

- **O payload não leva o raciocínio da implementação.** Este contexto tem tudo em memória e vai querer explicar as escolhas; a lista do que não entra está no Passo 4 da review
- **O veredicto é o do verificador.** Transcrever os `R-XX` com a severidade que ele devolveu. Rebaixar um `Bloqueante` porque "o código está certo" é o autor revisando a si mesmo pela porta dos fundos — discordância vai para o chat, ao lado do relatório, e a decisão é do usuário

Não encadear a review quando a tarefa terminou `Bloqueado` ou quando a execução parou esperando confirmação do usuário.

## O que NÃO fazer

- **Executar mais de uma tarefa por invocação.** Uma tarefa, uma revisão. Se a próxima parecer trivial, ela ainda assim tem critério de aceite próprio e review próprio.
- **Levar para o diff mudança que a tarefa não fez.** O diff da working tree (`git diff HEAD`) é o que o verificador lê: mudança em paralelo viraria finding sem contexto no review.
- **Fazer commits, branches, merges ou push.** A permissão `bash` de `opencode.json` nega; e o versionamento é decisão do usuário.
- **Implementar regra de negócio que não está em nenhuma `RN-XX` da tarefa.** Se a implementação exigir uma decisão que o PRD não cobre, parar e apontar a lacuna — o PRD é que precisa mudar, não o código que precisa adivinhar.
- **Marcar `Concluído` com critério de aceite parcialmente atendido.** Usar `Bloqueado` com o motivo em `**Bloqueio:**`.
- **Reescrever o plano durante a execução.** Se a tarefa está mal dimensionada, apontar e sugerir revisar via `planner-leanwork`. Executar uma tarefa diferente da planejada quebra a rastreabilidade silenciosamente.
- **Pular os testes porque "a mudança é pequena".** O critério de conclusão é o que está escrito na tarefa.

## Regra de ouro

O plano é a fonte de verdade do estado, e este comando é o único que o atualiza durante a execução. Um estado que o plano não declara não existe para o pipeline: `/leanwork-pipeline` e `/leanwork-trace` leem esse arquivo e nada mais. Deixar o plano desatualizado é mais custoso que não ter plano, porque os dois comandos passam a descrever um projeto que não existe.
