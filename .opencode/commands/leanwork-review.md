---
description: Revisa implementação contra plano + PRD + arquitetura num sub-agente de contexto limpo. Gera relatório REVIEW-{T-XX} com findings R-XX e devolve bloqueios ao plano. Sem commits nem merges automáticos.
---

# Review de Implementação SDD

Você acaba de invocar o reviewer do pipeline SDD Leanwork (fonte oficial `D:/Projetos/leanwork-sdd-v2`, somente leitura). O objetivo é validar que a implementação entrega o que `T-XX` prometeu, cruzando código com plano, PRD e arquitetura.

O `/leanwork-execute` encadeia esta review sozinho ao concluir uma tarefa. Chamado diretamente, serve ao round seguinte de uma review bloqueada, a mudança externa que o usuário quer revisar e a tarefa executada fora do pipeline.

Input do usuário: $ARGUMENTS

**Divergências declaradas deste projeto (adaptadas do original):**

- Sem branch de tarefa: o diff padrão sob review é a working tree (`git diff HEAD` + arquivos não rastreados tocados). Quando o usuário indicar branch, PR ou hash, o diff é o que ele indicar, e o Passo 7 não se aplica
- Sem commits e sem integração automática: a permissão `bash` de `opencode.json` nega `git add/commit/merge/switch`. O fechamento é relatório + plano atualizado; versionar é decisão do usuário
- Python ainda não autorizado: o gate roda manualmente (ou com Python, após decisão do usuário), e a ausência de gate automático **precisa ser anunciada no chat** — regra de "Degradação silenciosa" do README oficial

## O que fazer

### Passo 1 — Identificar a tarefa e o diff

Se o usuário forneceu `T-XX`, use. Caso contrário, descobrir em ordem:

1. **Última tarefa `Em andamento` ou `Concluído` sem review** no plano de `docs/plans/PLAN-*.md`
2. **Última entrada no chat** do `/leanwork-execute` nesta sessão
3. **Pergunta direta**: "Qual tarefa do plano essa implementação entrega? (ex.: T-04). E como acesso o código? (paste do diff, hash, branch para comparar, ou caminho dos arquivos)"

**Com working tree**: o diff sob review é `git diff HEAD` mais os arquivos não rastreados listados por `git status --porcelain`. Mudança rastreada não explicada pela tarefa entra no diff — avisar o usuário antes de seguir (é o custo declarado da operação em `main`).

**Com hash/PR/branch fornecidos pelo usuário**: o diff é o que ele indicar (`git show <hash>`, `git diff <base>...<branch>` — leitura é permitida; criar branch não é).

Identificada a tarefa, **procurar reviews anteriores dela**: `docs/reviews/REVIEW-{T-XX}-*.md`. Se existir algum, esta review é o round N+1 — ver "Review de segundo round" nos casos especiais e conduzir por lá. Essa verificação faz parte do fluxo normal, não é exceção.

### Passo 2 — Carregar contexto do projeto

Procure nos locais convencionais:

- Plano: `docs/plans/PLAN-*.md` que contém a tarefa T-XX
- PRD: referenciado no cabeçalho do plano (campo `**PRD de referência:`). Plano de trilha rápida não tem PRD, e isso não é lacuna: os `CA-XX` estão no bloco `**Cenários:**` da tarefa
- Arquitetura: `docs/architecture/proposta-arquitetural.md`
- Instruções do agente: `AGENTS.md` da raiz (o `CLAUDE.md` do original)

Se algum não existir, **registre como lacuna no relatório** e continue com review degradado. Não invente.

### Passo 3 — Descobrir a stack

Use a cascata em `D:/Projetos/leanwork-sdd-v2/skills/reviewer-leanwork/references/stack-detection.md`:

1. Proposta arquitetural (seção 6.2 Containers + Restrições)
2. `AGENTS.md` do projeto
3. Inspeção do repositório (`*.csproj`, `package.json`, etc.)
4. Pergunta ao usuário

Documente a stack descoberta e a fonte no preâmbulo do relatório. Neste projeto: C# / .NET 10, ASP.NET Core Razor Pages; catálogo de critério (`AGENTS.md`, linha `Catálogo:`): `D:/Projetos/leanwork-sdd-v2/stacks/dotnet/boas-praticas/`.

### Passo 4 — Despachar o verificador num contexto limpo

A review roda num **sub-agente fresco** via `task` tool (`subagent_type: reviewer-leanwork`, definido em `.opencode/agent/reviewer-leanwork.md`). A razão é direta: se `/leanwork-execute` acabou de implementar nesta conversa, quem revisa tem o raciocínio da implementação em memória e tende a confirmar a própria intenção em vez de ler o que o código faz. Autor e verificador precisam ser contextos diferentes.

O agente é read-only sobre a árvore do projeto (`edit: deny`) e instruído a aplicar a skill `reviewer-leanwork` (de `.opencode/skills/reviewer-leanwork/SKILL.md`) e a devolver os findings — não a escrever arquivo nenhum. A única escrita permitida é o **scratch do sensor de discriminação** (eixo 4) fora da árvore do projeto, em `C:\Users\augus\AppData\Local\Temp\opencode` — o protocolo de `D:/Projetos/leanwork-sdd-v2/skills/reviewer-leanwork/references/discrimination-sensor.md` é o que impede a mutação de alcançar a worktree do usuário.

**Payload do verificador.** O sub-agente não herda esta conversa, então o payload precisa ser autossuficiente: nada nele pode dizer "a tarefa que acabamos de discutir". Enumerar, item a item:

1. A tarefa `T-XX` **copiada na íntegra** do plano — campos, critérios de aceite e testes a escrever, não o resumo
2. O **texto integral** de cada `RN-XX`, `CA-XX`, `ADR-XX`, `UI-XX` que a tarefa declara, copiado do artefato de origem. Referência por código não serve: o sub-agente não tem o PRD em contexto
3. O diff sob review — saída de `git diff HEAD` + lista de não rastreados (ou o diff indicado pelo usuário) e como ler os arquivos
4. A stack detectada **e a fonte dela** (Passo 3)
5. Os padrões lidos do `AGENTS.md` da raiz (e do módulo afetado, quando houver)
6. Quando for round N+1, o relatório do round anterior
7. **Sem commits nesta configuração**, enviar literalmente: "sem commit — trailer não verificável". É o insumo do eixo 2: o verificador interpreta, não reconfere à mão
8. A instrução de **executar o sensor de discriminação** do eixo 4, com profundidade derivada das `RN-XX` de `Implementa:` — é o verificador quem roda, porque é ele quem tem o código em contexto

O sensor exige capacidade de executar comandos (`dotnet build`, runner de testes quando existir). Se o sub-agente não tiver essa capacidade no ambiente, não improvisa: devolve a seção "Sensor de discriminação" registrando que não rodou e por quê. Ausência declarada é informação; seção omitida é silêncio que passa por aprovação.

**O que NÃO entra no payload, e por quê:**

- **O raciocínio da implementação** — como a solução foi pensada, que alternativas foram descartadas
- **As justificativas de quem executou** — "fiz assim porque o handler já fazia X"
- **Qualquer narrativa sobre por que uma escolha foi feita**

Um verificador que recebe a justificativa julga se ela convence; um verificador que recebe só o spec e o código julga se o código cumpre o spec — que é a pergunta do review.

O sub-agente devolve os findings `R-XX` com severidade. **Quem escreve o relatório, salva o arquivo e edita o plano é o orquestrador** (Passos 5 e 6, aqui). O orquestrador transcreve: severidade e recomendação são as que o verificador devolveu; discordância vai para o chat, nunca para o relatório.

**Fallback — modo degradado.** Se não houver capacidade de sub-agente disponível no ambiente, rodar a review inline **e avisar no chat que autor e verificador são o mesmo contexto nesta execução**. O aviso não é opcional: é a única coisa que impede uma review degradada de passar por verificação independente.

A skill `reviewer-leanwork` continua sendo a fonte do método — os 5 eixos, mais o 6º de conformidade de interface quando existe SPEC-UI — e o relatório segue o template dela (`D:/Projetos/leanwork-sdd-v2/skills/reviewer-leanwork/references/review-template.md`).

### Passo 5 — Salvar o relatório

Salvar em `docs/reviews/REVIEW-{T-XX}-{data-iso}.md` (segundo round: sufixo `-round{N+1}`). Se o projeto usa outra convenção, perguntar ao usuário.

**Gate — validar o relatório antes de entregar.** Com Python autorizado: `python "D:/Projetos/leanwork-sdd-v2/scripts/validate_review.py" docs/reviews/REVIEW-T-XX-AAAA-MM-DD.md` — exit diferente de zero é PARE, corrigir; `WARN` não reprova. Python está **negado** neste ambiente e o hook automático do original (PostToolUse) não existe no OpenCode: conferir à mão os três pontos do gate (`finding` com `arquivo:linha`; `cobertura` com asserção reproduzida; `Bloqueante` com recomendação `Bloqueado`) e **dizer no chat que o relatório não passou pelo gate automático** (regra de "Degradação silenciosa").

Após gerar o relatório, mostrar ao usuário:

- Recomendação final (`✅ Aprovado / ⚠️ Aprovado com ressalvas / ⛔ Bloqueado`)
- Contagem por severidade
- Caminho do arquivo gerado
- Os 1-3 findings mais críticos em destaque

### Passo 6 — Devolver o resultado ao plano

Se houver **pelo menos um finding `Bloqueante`**:

1. Mudar o campo `**Status:**` da `T-XX` no plano para `Bloqueado`
2. Preencher `**Bloqueio:**` com a referência ao relatório e aos findings — ex.: `R-01, R-03 (REVIEW-T-04-2026-06-15)`

Confirmar com o usuário antes de editar o plano, e avisar se a tarefa estava `Concluído`: essa é a inconsistência entre estado declarado e estado validado que o `/leanwork-trace` reporta como grave.

Sem findings bloqueantes, **não** mexer no plano — `Aprovado com ressalvas` não muda o estado da tarefa; findings `Importante` viram tarefa nova via `planner-leanwork` quando justificarem.

### Passo 7 — Fechamento

Esta configuração não integra branches e não committa: no fechamento, transcrever o veredicto no chat. Com `Bloqueante`: a correção continua na working tree e o próximo `/leanwork-review T-XX` é o round seguinte. Sem `Bloqueante`: sugerir ao usuário commitar o trabalho (mensagem sugerida com trailer `Refs: T-XX`, convenção em `D:/Projetos/leanwork-sdd-v2/templates/id-conventions.md`) — o commit e o push são dele, e o gate `check_commit.py` do original não se aplica (lacuna declarada no `AGENTS.md` → seção Integração SDD).

Com o fechamento feito, `/leanwork-pipeline` passa a sugerir a próxima tarefa.

## Casos especiais

### Review de mudança com múltiplas tarefas

Se o diff entrega mais de uma tarefa, gerar **um relatório por tarefa** e mostrar resumo agregado. Não misturar tudo num relatório só.

### Review de segundo round (re-review)

Se já existe `REVIEW-{T-XX}-*.md` para a mesma tarefa:

1. Contar os relatórios existentes da tarefa. O novo é o round N+1 (sem sufixo = round 1, `-round2`, ...)
2. Ler o **relatório mais recente** e preencher a seção "Round anterior" do template, item a item: cada `R-XX` marcado como resolvido, persistente ou não verificável
3. Salvar com `-round{N+1}`. Não editar o relatório anterior — o histórico de qualidade da tarefa é a sequência deles
4. Numerar os findings novos a partir de `R-01` **deste** relatório. Ao citar um finding fora dele, qualificar com o nome do arquivo (`R-01 (REVIEW-T-04-2026-06-15)`)

### Projeto sem artefatos SDD

Oferecer code review genérico mas **avisar que não há rastreabilidade** e sugerir rodar `/leanwork-pipeline` para projetos futuros.

## Regra de ouro

Não substitua a leitura humana da mudança — complemente. O review estruturado serve para **não esquecer de checar coisas óbvias** e para **manter rastreabilidade**. Decisões finais ficam com pessoas.
