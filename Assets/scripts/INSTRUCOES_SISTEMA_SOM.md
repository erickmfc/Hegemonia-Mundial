# Sistema de Som para Unidades Militares

## Visão Geral
O sistema de som para unidades foi implementado com suporte a todas as unidades militares do jogo: **Helicóptero**, **Avião**, **Tanque**, **Carro** e **Navio**.

## Scripts Criados/Modificados

### 1. **SomUnidade.cs** (NOVO)
Script principal que gerencia todos os sons de uma unidade.

**Características:**
- Som de motor dinâmico que varia com a velocidade
- Som de motor parado (idle)
- Som de tiro
- Som de explosão ao morrer
- Som de dano ao receber ataque
- Pitch (tom) dinâmico baseado na velocidade
- Configurações específicas para cada tipo de unidade

### 2. **SistemaDeDanos.cs** (MODIFICADO)
Adicionados eventos para notificar outros sistemas:
- `OnDano` - Disparado quando recebe dano
- `OnMorte` - Disparado quando morre

### 3. **SistemaDeTiro.cs** (MODIFICADO)
Integração com o sistema de som:
- Tenta usar `SomUnidade` primeiro
- Fallback para o sistema antigo de AudioSource

## Como Usar

### Passo 1: Adicionar o Script SomUnidade
1. Selecione a unidade (prefab ou objeto na cena)
2. Clique em "Add Component"
3. Procure por "Som Unidade"
4. O script irá adicionar automaticamente um `AudioSource` se não existir

### Passo 2: Configurar o Tipo de Unidade
No Inspector, defina o **Tipo de Unidade** (campo `tipoUnidade`):
- Helicoptero
- Aviao
- Tank
- Carro
- Navio

Cada tipo tem configurações pré-definidas:
- **Helicóptero**: Alcance 80m, pitch 0.9-1.3
- **Avião**: Alcance 150m, pitch 0.8-1.8
- **Tanque**: Alcance 60m, pitch 0.7-1.2
- **Carro**: Alcance 50m, pitch 0.8-1.5
- **Navio**: Alcance 100m, pitch 0.6-1.0

### Passo 3: Adicionar os Áudios
No Inspector, arraste os arquivos de áudio para:

#### Sons de Movimento:
- **Som Motor**: Som quando a unidade está em movimento
- **Som Parado**: Som quando está parada (idle)

#### Sons de Ação:
- **Som Tiro**: Som ao atirar (usado pelo SistemaDeTiro)
- **Som Explosão**: Som ao morrer/explodir
- **Som Dano**: Som ao receber dano

### Passo 4: Ajustar Volumes (Opcional)
- Volume Motor: 0-1 (padrão: 0.5)
- Volume Tiro: 0-1 (padrão: 0.8)
- Volume Explosão: 0-1 (padrão: 1.0)

### Passo 5: Configurações Avançadas (Opcional)
- **Pitch Min/Max**: Tom do som em diferentes velocidades
- **Velocidade Para Max Pitch**: Velocidade necessária para atingir o pitch máximo
- **Loop Motor**: Se o som do motor deve fazer loop (geralmente sim)

## Funcionamento Automático

### Som de Motor
- Inicia automaticamente quando a unidade se move
- Ajusta o pitch (tom) baseado na velocidade
- Troca para som de idle quando para

### Som de Tiro
- Chamado automaticamente pelo `SistemaDeTiro` quando atira
- Variação aleatória leve no pitch (0.9-1.1)

### Som de Dano
- Chamado automaticamente pelo `SistemaDeDanos` quando recebe dano
- Variação aleatória no pitch (0.8-1.2)

### Som de Explosão
- Chamado automaticamente quando a unidade morre
- Usa o `AudioSource` secundário para não interferir com outros sons

## Integração com Sistemas Existentes

### SistemaDeTiro
O `SistemaDeTiro` agora procura primeiro por `SomUnidade` no pai:
```csharp
var somUnidade = GetComponentInParent<SomUnidade>();
if (somUnidade != null)
{
    somUnidade.TocarSomTiro();
}
```

### SistemaDeDanos
O `SistemaDeDanos` dispara eventos que o `SomUnidade` escuta:
```csharp
sistemaDanos.OnDano += TocarSomDano;
sistemaDanos.OnMorte += TocarSomExplosao;
```

## Exemplo de Configuração

### Para um Tanque:
1. Tipo Unidade: `Tank`
2. Som Motor: `tank_engine_loop.wav`
3. Som Parado: `tank_idle.wav`
4. Som Tiro: `tank_cannon.wav`
5. Som Explosão: `tank_explosion.wav`
6. Som Dano: `metal_hit.wav`

### Para um Helicóptero:
1. Tipo Unidade: `Helicoptero`
2. Som Motor: `helicopter_rotor.wav`
3. Som Parado: `helicopter_idle.wav`
4. Som Tiro: `minigun.wav`
5. Som Explosão: `helicopter_crash.wav`
6. Som Dano: `metal_hit.wav`

## Sistema de AudioSource Dual

O script cria automaticamente **dois AudioSources**:

1. **AudioSource Principal**: Para o som do motor (loop contínuo)
2. **AudioSource Secundário**: Para efeitos (tiro, explosão, dano)

Isso garante que:
- O som do motor não seja interrompido quando atira
- Múltiplos efeitos podem tocar simultaneamente
- Sons 3D com diferentes alcances

## Detecção de Velocidade

O sistema detecta a velocidade da unidade de três formas:

1. **NavMeshAgent** (unidades terrestres)
2. **Rigidbody** (unidades com física)
3. **Mudança de Posição** (unidades aéreas/manuais)

Isso garante compatibilidade com todos os tipos de movimento.

## Troubleshooting

### Som não toca:
- Verifique se o AudioClip está atribuído
- Verifique se o volume não está em 0
- Verifique se a câmera tem um AudioListener

### Som do motor não muda com velocidade:
- Verifique se a unidade tem NavMeshAgent ou Rigidbody
- Ajuste "Velocidade Para Max Pitch" para um valor mais baixo

### Som de tiro não toca:
- Verifique se a unidade tem `SistemaDeTiro`
- Verifique se o Som Tiro está atribuído
- Verifique se o script está no mesmo GameObject ou no pai

## Chamadas Manuais (Opcional)

Você pode chamar os métodos manualmente de outros scripts:

```csharp
var somUnidade = GetComponent<SomUnidade>();
somUnidade.TocarSomTiro();
somUnidade.TocarSomDano();
somUnidade.TocarSomExplosao();
```
