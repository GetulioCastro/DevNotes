# DevNotes

Biblioteca pessoal de conhecimentos em C#/.NET, desenvolvimento de software e IA Agêntica.

## Comandos

- Build: `dotnet build DevNotes.slnx`
- Executar: `dotnet run --project src/DevNotes.Web`

<!-- leanwork-context:build:start -->
<!-- TODO: comandos de test serão adicionados quando o projeto de testes existir. -->
<!-- leanwork-context:build:end -->

## Stack

- C# / .NET 10 (`net10.0`), ASP.NET Core Razor Pages — `src/DevNotes.Web`
- Nullable enable, ImplicitUsings enable

## Segurança e desempenho

Catálogo: D:/Projetos/leanwork-sdd-v2/stacks/dotnet/boas-praticas/ — exceto: nenhum

## Integração SDD Leanwork

Pipeline Spec-Driven Development da fonte oficial `D:/Projetos/leanwork-sdd-v2` (somente leitura), integrado para este runtime via `/leanwork-pipeline`, `/leanwork-execute`, `/leanwork-review` e `/leanwork-trace` (comandos em `.opencode/commands/`). Estado do pipeline: `docs/plans/`, `docs/reviews/`, `docs/architecture/`, `docs/prds/`, `docs/prototype/` (quando existirem).

Operação em `main` direto na working tree: sem branches de tarefa, sem commits, merges ou pushes automáticos — `git` é somente consulta.

<!-- leanwork-context:start -->
<!-- Bloco gerenciado pela skill context-leanwork. Conteúdo escrito à mão fora destes marcadores nunca é sobrescrito. -->
<!-- leanwork-context:end -->
