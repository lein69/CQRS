# C4 Model

The diagrams describe the implemented demo boundary: students and group owners use a browser, command and query APIs run separately, and Kafka connects SQL write state to MongoDB read projections. CommandService stores registered accounts and issues JWTs, which both APIs use for authentication. There is no API Gateway or external identity provider.

## Level 1 - System Context

<!-- mermaid-checked: no \n, no em-dash/en-dash, no {} in labels, subgraphs are id["label"], arrows are -->|"label"|, all subgraphs closed by end, ids unique -->
```mermaid
flowchart TD
    c1Student["Student"]
    c1Owner["Study group owner"]
    subgraph c1Boundary["Study group management system"]
        c1System["Study group microservices and demo UI"]
    end

    c1Student -->|"searches groups and requests membership"| c1System
    c1Owner -->|"creates groups and reviews requests"| c1System
```

## Level 2 - Containers

<!-- mermaid-checked: no \n, no em-dash/en-dash, no {} in labels, subgraphs are id["label"], arrows are -->|"label"|, all subgraphs closed by end, ids unique -->
```mermaid
flowchart TD
    c2Student["Student"]
    c2Owner["Study group owner"]
    subgraph c2System["Study group management system"]
        c2Browser["Browser UI served by CommandService"]
        c2Command["CommandService ASP.NET Core API and authentication"]
        c2Query["QueryService ASP.NET Core API"]
        c2Consumer["ConsumerService worker"]
        c2Sql[("SQL Server accounts and write model")]
        c2Kafka[("Kafka")]
        c2Mongo[("MongoDB read models")]
        c2Zoo["ZooKeeper"]
    end

    c2Student -->|"uses"| c2Browser
    c2Owner -->|"uses"| c2Browser
    c2Browser -->|"registers, signs in, and sends commands"| c2Command
    c2Browser -->|"queries"| c2Query
    c2Command -->|"writes group data and outbox"| c2Sql
    c2Command -->|"outbox publisher"| c2Kafka
    c2Kafka -->|"events"| c2Consumer
    c2Consumer -->|"updates projections"| c2Mongo
    c2Query -->|"reads projections"| c2Mongo
    c2Zoo -->|"coordinates"| c2Kafka
```

## Level 3 - Key Components

<!-- mermaid-checked: no \n, no em-dash/en-dash, no {} in labels, subgraphs are id["label"], arrows are -->|"label"|, all subgraphs closed by end, ids unique -->
```mermaid
flowchart LR
    subgraph c3Command["CommandService components"]
        c3AuthController["AuthenticationController"]
        c3AuthService["AuthenticationService"]
        c3CommandController["GroupsController"]
        c3GroupService["StudyGroupService"]
        c3Outbox["OutboxPublisher"]
        c3WriteDb[("SQL accounts and write tables")]
    end
    subgraph c3Messaging["Event transport"]
        c3Kafka[("Kafka")]
    end
    subgraph c3Consumer["ConsumerService components"]
        c3Worker["Worker"]
        c3Handler["StudyGroupCreatedHandler"]
        c3ReadDb[("MongoDB projections")]
    end
    subgraph c3Query["QueryService components"]
        c3QueryController["GroupsController"]
        c3Repository["StudyGroupRepository"]
    end

    c3AuthController -->|"account requests"| c3AuthService
    c3AuthService -->|"stores account and password hash"| c3WriteDb
    c3CommandController -->|"commands"| c3GroupService
    c3GroupService -->|"SQL transaction and outbox"| c3WriteDb
    c3Outbox -->|"loads pending events"| c3WriteDb
    c3Outbox -->|"publishes"| c3Kafka
    c3Kafka -->|"delivers"| c3Worker
    c3Worker -->|"dispatches"| c3Handler
    c3Handler -->|"upserts"| c3ReadDb
    c3QueryController -->|"queries"| c3Repository
    c3Repository -->|"reads"| c3ReadDb
```

### Container Responsibilities

| Container | Responsibility | Data boundary |
|---|---|---|
| Browser UI | Group creation, search, membership request, owner review | JWT kept in tab-scoped sessionStorage; no domain data stored locally |
| CommandService | Registration/login, JWT issuance, synchronous write API, and outbox publishing | SQL Server `WriteDB` accounts and group data |
| ConsumerService | Asynchronous event consumption and projection updates | MongoDB read collections |
| QueryService | Read-only group and request API | MongoDB read collections |
| Kafka and ZooKeeper | Event transport and broker coordination | Event messages and offsets |
