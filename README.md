# Matching

## Pré-requisitos para desenvolvimento Windows:

- Ter instalado o **wsl-2** , **docker** e o **docker-compose** na sua máquina. 

- Criar a rede virtual:

```
docker network create dm-network
```

## Para subir a aplicação:

- Baixar **dev.zip** do *s3/sl-containesr-resource-dev/matching*

- Colocar **.env** na raíz da aplicação e fazer modificações necessárias

- Colocar diretório **config** na raíz da aplicação

- Entrar no diretório matching

- Criar diretório Logs, Session e data na raiz da aplicação

```
mkdir Logs
mkdir Session
mkdir data
```

- Executar o compose

```
docker-compose -f docker-compose.yml up -d
```

Obs: Os instrumentos de testes TESTE1-SL, TESTE2-SL e TESTE3-SL foram criados para testes

Pronto, agora você será capaz de se conectar nas portas 6684 para enviar ofertas e na 6685 para receber a difusão do mercado.

- Remover os diretórios /obj e /bin existentes

- Remover se necessário a imagem para ser reconstruiída.

- Executar o compose

```
docker-compose -f docker-compose-local.yml up
```

## Para baixar a aplicação:

```
docker-compose -f docker-compose-local.yml down
```

## Para testar a aplicação:

```
docker-compose -f docker-compose-test.yml up

docker build . -f Dockerfile.test -t sltools-matching-test

docker run --env-file .env --network="dm-network" --name matching-test sltools-matching-test
```

# Para fazer deploy da aplicação

- Alterar versão da aplicação nos seguintes arquivos

```
buildspec.yml 
deploy/scripts/setup.sh
deploy/docker-compose.yml
```