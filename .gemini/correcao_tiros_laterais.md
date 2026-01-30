# Correção: Problema de Tiros Laterais

## Problema Identificado
As unidades (tanques, torretas e navios) estavam atirando de lado, antes de estarem completamente alinhadas com o alvo. Isso acontecia porque a verificação de ângulo antes de disparar era muito permissiva.

## Correções Aplicadas

### 1. **ControleTorreta.cs** (Linha 201)
- **Antes:** Tolerância de 10 graus de erro
- **Depois:** Tolerância de 5 graus de erro
- **Resultado:** Torretas de navios agora só atiram quando estão perfeitamente alinhadas

### 2. **SistemaDeTiro.cs** (Linha 87)
- **Antes:** Tolerância de 15 graus de erro
- **Depois:** Tolerância de 10 graus de erro
- **Resultado:** Tanques e soldados atiram com mais precisão

### 3. **SistemaDeTiro.cs** (Linha 77)
- **Antes:** Velocidade de rotação de 30 graus/segundo
- **Depois:** Velocidade de rotação de 45 graus/segundo
- **Resultado:** Unidades giram mais rápido para se alinhar com o alvo

### 4. **Torreta.cs** (Linha 71)
- **Adicionado:** Verificação de ângulo antes de disparar (8 graus)
- **Antes:** Atirava sem verificar alinhamento
- **Depois:** Só atira se estiver bem alinhada com o alvo

## Como Funciona

Agora todas as unidades verificam se estão apontando diretamente para o alvo antes de atirar. O código calcula o ângulo entre a direção que a unidade está apontando (`forward`) e a direção do alvo, e só permite o disparo se esse ângulo for menor que:

- **Torretas navais (ControleTorreta):** < 5° 
- **Torretas simples (Torreta):** < 8°
- **Tanques e soldados (SistemaDeTiro):** < 10°

## Teste no Unity

Para testar se funcionou:
1. Selecione um tanque ou navio
2. Dê ordem de ataque a um inimigo
3. Observe que a unidade vai **girar completamente** antes de atirar
4. Os tiros agora devem sair **direto para frente** (na direção do cano/torreta)

## Ajustes Finos (Se necessário)

Se ainda achar que está atirando um pouco de lado, você pode:
- Reduzir ainda mais os valores de tolerância angular (ex: 3° para navios, 5° para tanques)
- Aumentar a `velocidadeGiro` nas configurações da torreta no Inspector do Unity
