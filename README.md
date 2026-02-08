# Desafio Técnico PagueVeloz: Sistema de Transações Financeiras

Este repositório contém a implementação do desafio técnico para o sistema de processamento de transações financeiras da PagueVeloz. O projeto foi construído focando em **DDD (Domain-Driven Design)**, alta coesão, baixo acoplamento e resiliência.

---

## Arquitetura e Decisões Técnicas

A solução adota uma abordagem de **DDD (Domain-Driven Design)**, dividindo a aplicação em camadas concêntricas para isolar o domínio das regras de negócio de detalhes de infraestrutura (banco de dados, frameworks, UI).

### Estrutura do Projeto
- **PagueVeloz.Domain**: O núcleo do domínio. Contém as entidades (`Account`, `Transaction`), objetos de valor, interfaces de repositório e regras de negócio puras. Não depende de nenhum framework externo.
- **PagueVeloz.Application**: Camada de aplicação que orquestra os casos de uso (Commands/Queries). Responsável pela "dança" entre as entidades e os repositórios.
- **PagueVeloz.Infrastructure**: Implementação dos detalhes técnicos. Aqui residem o `DbContext` (EF Core), configurações de banco de dados, implementações de repositórios e integrações externas.
- **PagueVeloz.API**: A camada de apresentação (Entrypoint), expondo os endpoints REST e gerenciando a injeção de dependência.

### Justificativas Técnicas
- **Serilog**: Biblioteca de logging estruturado, essencial para observabilidade em ambientes distribuídos, permitindo buscas ricas em logs (ex: filtrar por `TransactionId`).
- **Polly**: Biblioteca de resiliência utilizada para implementar políticas de **Retry** (tentar novamente) e **Circuit Breaker** (interromper chamadas falhas) em operações críticas ou instáveis.
- **Grafana**: Plataforma de observabilidade que permite visualizar métricas de negócio e performance.
- **InfluxDB**: Banco de dados de métricas que permite armazenar e visualizar métricas de negócio e performance.
- **K6**: Ferramenta de testes de carga que permite validar a performance sob estresse.

---

## Como Executar o Projeto

### Pré-requisitos
- **Docker** e **Docker Compose** instalados.
- **.NET SDK 9.0** (opcional, caso queira rodar fora do Docker).

### Usando Docker (Recomendado)
A maneira mais simples de rodar tudo (API + Banco + Monitoramento) é via Docker Compose.

```bash
docker compose --profile default up --build
```

1. Aplique as migrações (se necessário):
   ```bash
   dotnet ef database update --project src/PagueVeloz.Infrastructure --startup-project src/PagueVeloz.API
   ```

A API estará disponível em: `http://localhost:5000`
Documentação Swagger: `http://localhost:5000/swagger`


## Testes

### Testes de Unidade e Integração
O projeto inclui testes cobrindo as regras de domínio e fluxos principais.

```bash
dotnet test
```

### Testes de Carga (K6)
Para validar a performance sob estresse, incluímos um ambiente completo de testes de carga com **K6**, **InfluxDB** e **Grafana**.

Para rodar os testes de carga e ver os gráficos em tempo real:

1. Execute o perfil de testes:
   ```bash
   docker compose --profile k6 up --build
   ```
   *Isso subirá a API, Banco, K6, InfluxDB e Grafana.*

2. Acompanhe os resultados no Grafana:
   - Acesse: `http://localhost:3000`
   - Dashboard: **PagueVeloz - k6 Load Test** (já configurado)

O teste executa cenários de Smoke, Ramp Up (aumento de carga) e Spike (pico de acesso), validando criação de contas, transações e concorrência.

---

## API Endpoints

Abaixo alguns exemplos de como interagir com a API.

### Contas

**Criar Conta**
```http
POST /api/accounts
Content-Type: application/json

{
  "client_id": "CLIENT-001",
  "initial_balance": 1000,
  "credit_limit": 500,
  "currency": "BRL"
}
```

**Consultar Saldo**
```http
GET /api/accounts/{accountId}/balance
```

### Transações

Operações suportadas: `credit`, `debit`, `reserve`, `capture`, `reversal`, `transfer`.

**Realizar Transação**
```http
POST /api/transactions
Content-Type: application/json

{
  "operation": "credit",
  "account_id": "{accountId}",
  "amount": 10000,
  "currency": "BRL",
  "reference_id": "TXN-UNIQUE-001",
  "metadata": { "description": "Depósito inicial" }
}
```

> **Nota**: Valores monetários são sempre inteiros (centavos). Ex: R$ 10,00 = `1000`.

---

## Observabilidade e Monitoramento

- **Health Checks**: `GET /health` para verificar a saúde da aplicação e conexão com banco.
- **Logs**: Logs estruturados são gerados no console (e podem ser enviados para ferramentas como Elasticsearch/Seq).
- **Métricas**: Métricas de negócio e performance são coletadas durante os testes de carga e visualizadas no Grafana.

---

##  Decisões de Negócio Extras

- **Concorrência**: Implementado `Optimistic Locking` (bloqueio otimista) para garantir que transações concorrentes na mesma conta não gerem inconsistência de saldo.
- **Idempotência**: O campo `reference_id` é chave única de negócio. Transações repetidas com mesmo ID são rejeitadas ou retornam o sucesso anterior (dependendo da implementação exata), garantindo que não haja duplicidade de cobrança.
