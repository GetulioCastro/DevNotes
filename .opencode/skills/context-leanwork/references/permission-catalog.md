# Catálogo de Permissões — `opencode.json` (OpenCode)

Receitas de `allow` / `ask` / `deny` por ecossistema, usadas pela skill `context-leanwork` ao compor a chave `permission` do `opencode.json`. Adaptado do catálogo original para Claude Code (`D:/Projetos/leanwork-sdd-v2/skills/context-leanwork/references/permission-catalog.md`) — as regras do OpenCode têm semântica diferente, e a diferença é central para a receita ficar correta.

## Como as regras funcionam (OpenCode)

- **Formato:** `permission` é um objeto por tool (`read`, `edit`, `glob`, `grep`, `list`, `bash`, `task`, `external_directory`, ...). Cada tool vale uma ação ("allow" no plural para todo o tool) ou um objeto `{ padrão: ação }`.
- **Objeto por padrão:** no `bash`, os padrões são globs sobre o comando (`"git status*": "allow"`). Ações possíveis: `"allow"`, `"ask"`, `"deny"`.
- **A ÚLTIMA regra que casar decide** — e a especificidade não desempata. Isto inverte a intuição do Claude Code: aqui a regra estreita precisa vir **depois** da larga, não antes. Ordem de inserção importa.
- **Escopos:** `opencode.json` do projeto e `~/.config/opencode/opencode.json` (global) são deep-merged — projeto sobrescreve global. **Não há garantia de "deny vence sempre"**: um `allow` posterior no objeto (ou no projeto) afrouxa um `deny` mais cedo. O rigor depende da ordem escrita e da revisão do diff pela skill.
- **Alguns tools só aceitam ação única:** `todowrite`, `question`, `webfetch`, `websearch` — sem objeto por padrão.
- **Fronteira de espaço nos globs:** o espaço faz parte do padrão — `git status*` casa com `git status` e `git status --porcelain`; `git s*` casaria com `show` e `stage`. Escrever o prefixo completo e conferir as variantes: `git push --force*` não casa `--force-with-lease` (o `-` vem em vez do espaço). Quando o perigo é uma flag em posição variável, cobrir as variantes conhecidas explicitamente — ver a receita de Git.

### Regra larga vs regra estreita

Aqui a lógica é a inversa do original: uma regra estreita no **fim** do objeto vence uma larga anterior. Logo, `{"*": "ask", "dotnet build*": "allow"}` funciona: `dotnet build` é permitido sem confirmação, todo o resto pergunta. Uma linha larga no fim (`"*": "ask"` por último) **apagaria** todas as exceções anteriores — sempre conferir que nenhum padrão universal (`*`, `git *`, `npm *`) fica depois das exceções.

**Como conferir uma receita:** ler o objeto de cima para baixo e, para cada regra, procurar as que vêm depois dela e casam com o mesmo comando — a última vence. Se a regra larga vencer uma estreita por estar depois, reordenar.

## Onde cada coisa mora

| Arquivo | Escopo | Versionado | Uso |
|---|---|---|---|
| `opencode.json` / `opencode.jsonc` (raiz do projeto) | Projeto | **Sim** — viaja com o repo | Política do time |
| `~/.config/opencode/opencode.json` | Usuário | Não | Preferências pessoais em todos os projetos |

A skill escreve **apenas** o `opencode.json` do projeto. Preferências individuais são do dev, não do projeto. Não há equivalente do `.claude/settings.local.json`: overrides pessoais vivem no config global do usuário.

---

## Deny universal — vale para qualquer stack

Esta lista é o núcleo de segurança e entra em **todo** projeto, independente de tecnologia. São arquivos que carregam segredo em texto puro e comandos destrutivos. Em `opencode.json` (uma chave `permission` só; ordem interna: universal primeiro, stack depois, exceções por último):

```jsonc
"permission": {
  "read": {
    // Variáveis de ambiente
    "**/.env": "deny",
    "**/.env.*": "deny",

    // Chaves e certificados
    "**/*.pem": "deny",
    "**/*.key": "deny",
    "**/*.pfx": "deny",
    "**/*.p12": "deny",
    "**/id_rsa*": "deny",
    "**/id_ed25519*": "deny",

    // Credenciais de cloud e infra
    "**/.aws/credentials": "deny",
    "**/.kube/config": "deny",
    "**/*.kubeconfig": "deny",
    "**/terraform.tfstate": "deny",
    "**/terraform.tfstate.*": "deny",
    "**/*.tfvars": "deny",

    // Tokens de registry
    "**/.npmrc": "deny",
    "**/.pypirc": "deny",
    "**/.docker/config.json": "deny"
  },
  "bash": {
    // Comandos destrutivos
    "rm -rf *": "deny",
    "git reset --hard*": "deny",

    // Force push — forma longa, curta e flag depois do remote
    "git push --force*": "deny",
    "git push -f*": "deny",
    "git push * --force": "deny",
    "git push * --force *": "deny",
    "git push * -f": "deny",
    "git push * -f *": "deny",

    // Padrão de fundo
    "*": "ask"
  }
}
```

**Sobre `terraform.tfstate`:** costuma ser esquecido e é dos piores — guarda senha de banco, chave de API e token em texto puro. Sempre incluir quando o projeto tem Terraform.

**Sobre `.npmrc` e `.pypirc`:** carregam token de publicação em registry. Ler é o primeiro passo para vazar.

---

## .NET

```jsonc
"bash": {
  "dotnet add package*": "ask",
  "dotnet ef migrations add*": "ask",
  "dotnet ef database update*": "ask",
  "dotnet nuget push*": "deny",
  "dotnet ef database drop*": "deny",
  "dotnet build*": "allow",
  "dotnet test*": "allow",
  "dotnet run*": "allow",
  "dotnet restore*": "allow",
  "dotnet format*": "allow",
  "dotnet ef migrations list*": "allow",
  "dotnet ef migrations script*": "allow"
}
```

**Notas de calibragem:**
- Ordem importa: regras de `deny` e `ask` antes, `allow` por último aqui é deliberado — no OpenCode a última que casa vence; confira ao unir com o universal que nenhum `*` largo fique por último.
- `dotnet ef migrations script` e `list` são leitura — seguros. `add` e `database update` alteram estado — confirmação.
- `dotnet ef database drop` é destrutivo e irreversível: `deny`, não `ask`.
- `appsettings.Production.json` e `secrets.json`: acrescentar ao `read` do deny universal se o projeto os usa (`"**/appsettings.Production.json": "deny"`, `"**/secrets.json": "deny"`).
- `dotnet add package` confirmado protege contra supply chain — instalação silenciosa é vetor real.

## Node.js / TypeScript

```jsonc
"bash": {
  "npm install*": "ask",
  "npm i*": "ask",
  "npx *": "ask",
  "npm publish*": "deny",
  "npm run build*": "allow",
  "npm run test*": "allow",
  "npm run lint*": "allow",
  "npm run dev*": "allow",
  "npm ci*": "allow"
}
```

**Adaptar ao gerenciador detectado pelo lockfile:** `pnpm-lock.yaml` → trocar `npm` por `pnpm`; `yarn.lock` → `yarn`; `bun.lockb` → `bun`. Usar o gerenciador errado gera regra que nunca casa.

**Sobre `npx`:** executa pacote arbitrário baixado na hora — sempre confirmação. No OpenCode é possível excecionar (`"npx tsc *": "allow"` **depois** de `"npx *": "ask"` vence pela posição), mas o padrão da skill é não excecionar: nomear script no `package.json` e liberar `npm run <script>*`.

## Python

```jsonc
"bash": {
  "pip install*": "ask",
  "poetry add*": "ask",
  "uv add*": "ask",
  "alembic upgrade*": "ask",
  "python manage.py migrate*": "ask",
  "twine upload*": "deny",
  "python manage.py flush*": "deny",
  "pytest*": "allow",
  "ruff*": "allow",
  "black*": "allow",
  "mypy*": "allow",
  "python -m pytest*": "allow"
}
```

Adaptar ao gerenciador detectado: `poetry.lock` → poetry; `uv.lock` → uv; `Pipfile.lock` → pipenv.

## Go

```jsonc
"bash": {
  "go get*": "ask",
  "go install*": "ask",
  "go build*": "allow",
  "go test*": "allow",
  "go run*": "allow",
  "go vet*": "allow",
  "go fmt*": "allow",
  "golangci-lint run*": "allow"
}
```

## Java / Kotlin

```jsonc
"bash": {
  "./gradlew publish*": "ask",
  "mvn deploy*": "ask",
  "./gradlew flywayMigrate*": "ask",
  "./gradlew build*": "allow",
  "./gradlew test*": "allow",
  "mvn compile*": "allow",
  "mvn test*": "allow"
}
```

Acrescentar no `read` do deny universal quando o projeto usa: `"**/*.jks": "deny"`, `"**/*.keystore": "deny"`.

## Rust

```jsonc
"bash": {
  "cargo add*": "ask",
  "cargo install*": "ask",
  "cargo publish*": "deny",
  "cargo build*": "allow",
  "cargo test*": "allow",
  "cargo run*": "allow",
  "cargo clippy*": "allow",
  "cargo fmt*": "allow"
}
```

## PHP

```jsonc
"bash": {
  "composer require*": "ask",
  "composer update*": "ask",
  "php artisan migrate*": "ask",
  "php artisan migrate:fresh*": "deny",
  "php artisan db:wipe*": "deny",
  "php artisan test*": "allow",
  "./vendor/bin/phpunit*": "allow",
  "./vendor/bin/pint*": "allow"
}
```

## Ruby

```jsonc
"bash": {
  "bundle add*": "ask",
  "bin/rails db:migrate*": "ask",
  "bin/rails db:drop*": "deny",
  "gem push*": "deny",
  "bundle exec rspec*": "allow",
  "bundle exec rubocop*": "allow",
  "bin/rails test*": "allow"
}
```

---

## Git — transversal a qualquer stack

Receita genérica ( projetos que usam o pipeline original usam estoque; ajustar ao projeto):

```jsonc
"bash": {
  "git push --force*": "deny",
  "git push -f*": "deny",
  "git push * --force": "deny",
  "git push * --force *": "deny",
  "git push * -f": "deny",
  "git push * -f *": "deny",
  "git reset --hard*": "deny",
  "git clean*": "deny",
  "git commit*": "ask",
  "git push*": "ask",
  "git merge*": "ask",
  "git rebase*": "ask",
  "git switch *": "ask",
  "git add *": "ask",
  "git stash*": "ask",
  "gh pr create*": "ask",
  "gh pr merge*": "ask",
  "git status*": "allow",
  "git diff*": "allow",
  "git log*": "allow",
  "git show*": "allow",
  "git branch --list *": "allow",
  "git branch -a*": "allow"
}
```

**Este projeto DevNotes é mais estrito que a receita:** o `opencode.json` da raiz **nega** `git add/commit/merge/switch/stash/push` (git somente consulta — regra do pipeline local). A receita fica aqui como ponto de partida para outros projetos gerados pela skill.

**Sobre `git commit` no `ask`:** decisão de estilo. Time que usa o agente para commits frequentes pode mover para `allow` — commit local é reversível. Perguntar ao usuário antes de gravar.

**Por que seis regras para negar force push.** Idem catálogo original: uma regra só não basta por causa da fronteira de espaço (`--force`, `-f`, flag depois do remote). `--force-with-lease` **não** casa com as duas primeiras (`-` no lugar do espaço) e cai no `ask`.

---

## Docker e infraestrutura local

```jsonc
"bash": {
  "docker compose down*": "ask",
  "docker build *": "ask",
  "docker push*": "ask",
  "docker system prune*": "deny",
  "docker volume rm*": "deny",
  "docker volume prune*": "deny",
  "kubectl delete *": "deny",
  "terraform apply*": "deny",
  "terraform destroy*": "deny",
  "docker compose up*": "allow",
  "docker compose stop*": "allow",
  "docker compose start*": "allow",
  "docker compose logs*": "allow",
  "docker ps*": "allow"
}
```

**Sobre `docker compose down` no `ask`:** `down -v` remove volumes — banco de desenvolvimento, seed, fixtures. Sem a flag, `up` recria containers e redes. O risco está no `-v`; negar apenas a flag seria preciso, mas a flag aparece em qualquer posição e a cobertura por prefixo falharia na primeira variante esquecida. Confirmação no `down` inteiro resolve em uma linha.

**Buracos que permanecem** (idem original): `docker compose up -V` descarta volumes anônimos (volume nomeado não afetado); `terraform apply` mexe em infra real — negado com consciência, time que adota IaC com agente move para `ask` deliberadamente.

---

## Princípios de calibragem

**Menor privilégio por padrão.** Preferir prefixo específico (`"dotnet build*"` ) a tool inteira, e nunca `"*": "allow"` no fundo do objeto.

**A pergunta que decide a ação:**

| Pergunta | Ação |
|---|---|
| É leitura ou operação local reversível? | `"allow"` |
| Altera estado externo, mas é recuperável? | `"ask"` |
| É irreversível, destrutivo, ou expõe segredo? | `"deny"` |

**Fadiga de confirmação é risco de segurança.** Regras `"ask"` grandes demais treinam o dev a aprovar no automático. Melhor `"allow"` generoso no que é seguro e `"deny"` firme no que é perigoso, com `"ask"` reservado ao que merece pausa.

**Ordem no OpenCode é o cerco.** Sem a garantia "deny vence sempre" do original, o rigor mora na ordem: negações antes, exceções deliberadas por último, e nenhum `*` largo no fim do objeto. A skill sempre mostra o diff final e o lê de cima para baixo antes de gravar.

**Não bloquear o que o pipeline SDD precisa.** O agente lê `docs/**` o tempo todo (PRDs, planos, ADRs). Nunca negar leitura de `docs/`.
