# Frontend — FileFlow

Este projeto contém a camada de interface do FileFlow, construída com Angular.

## Configuração básica

O frontend é configurado para rodar com o proxy de API definido em [proxy.conf.js](proxy.conf.js), o que permite que chamadas para `/api` sejam encaminhadas para o backend local.

## Comandos principais

### Instalar dependências

```bash
npm install
```

### Rodar localmente

```bash
npm start
```

Ou, alternativamente:

```bash
npx ng serve
```

A aplicação fica disponível em `http://localhost:4200/`.

### Build de produção

```bash
npm run build
```

### Testes

```bash
npm run test
```

## Configuração de proxy

O arquivo [proxy.conf.js](proxy.conf.js) redireciona chamadas para `/api` para a URL do backend definida por variáveis de ambiente, como:

- `services__api__https__0`
- `services__api__http__0`

Isso facilita a integração local entre frontend e API sem precisar trocar manualmente a base URL em cada chamada.

## Estrutura resumida

- [src/app](src/app) — componentes, serviços e módulos da aplicação.
- [src/styles.scss](src/styles.scss) — estilos globais.
- [angular.json](angular.json) — configuração do projeto Angular.
- [package.json](package.json) — dependências e scripts.

## Observação

Este README foi adaptado para refletir a estrutura atual do projeto e a integração com a API local do FileFlow.
