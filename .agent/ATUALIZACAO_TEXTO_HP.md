# 🩸 Sistema de Vida - ATUALIZAÇÃO: Texto de HP Adicionado!

## ✨ Nova Funcionalidade:

Agora a barra de vida mostra **os números de HP** em cima da barra!

### Exemplo Visual:
```
      [ 80/100 ]    ← Texto branco com sombra
   ████████░░░░     ← Barra amarela (80%)
```

---

## 🎯 O que mudou:

### ✅ **Atualização Automática:**
- O componente `CriarBarraDeVida` agora cria **automaticamente** um texto acima da barra
- Formato: **"80/100"** (vida atual / vida máxima)
- Texto branco com sombra preta para melhor leitura
- Atualiza em tempo real quando leva dano ou cura

---

## 🚀 Como Usar (Nada Mudou!):

### Passo 1: Adicionar em Unidade
```
1. Selecione a unidade
2. Add Component → CriarBarraDeVida
3. ✅ Pronto!
```

### Passo 2: Rode o Jogo
- A barra aparece com:
  - ✅ Barra colorida (verde/amarelo/vermelho)
  - ✅ **Texto "100/100" acima** ← NOVO!

---

## 📊 Exemplo Completo:

### Quando a unidade toma dano:
```
Vida Cheia (100%):
      [ 100/100 ]
   ████████████     Verde

Vida Média (50%):
      [ 50/100 ]
   ██████░░░░░░     Amarelo

Vida Baixa (20%):
      [ 20/100 ]
   ██░░░░░░░░░░     Vermelho
```

---

## 🔧 Customização:

### Se quiser mudar o tamanho do texto:
1. Durante o jogo, expanda a hierarquia da unidade
2. Vá em: `BarraDeVida → TextoVida`
3. No Inspector, ajuste `Font Size` (padrão: 14)

### Se quiser mudar a cor do texto:
1. Mesmo caminho acima
2. Ajuste `Color` no componente `Text`

---

## 🧪 Testando:

### Teste com TesteVida:
```
1. Adicione CriarBarraDeVida + TesteVida na unidade
2. Rode o jogo
3. Pressione [1] para dano
4. Observe:
   - ✅ Barra diminui
   - ✅ Cor muda
   - ✅ Texto atualiza (ex: "80/100")
```

---

## 📋 Estrutura da Barra (Atualizada):

```
Unidade
├─ Vida (script)
├─ CriarBarraDeVida (script)
└─ BarraDeVida (GameObject) ← Criado automaticamente
   ├─ Canvas (World Space)
   ├─ Fundo (Image)
   │  └─ Preenchimento (Image - Fill)
   ├─ TextoVida (Text) ← NOVO!
   │  └─ Shadow (efeito)
   └─ BarraDeVida (script)
```

---

## 🎨 Características do Texto:

- **Fonte:** Arial padrão do Unity
- **Tamanho:** 14px
- **Cor:** Branco com sombra preta
- **Alinhamento:** Centralizado
- **Posição:** Acima da barra
- **Formato:** "vidaAtual/vidaMaxima"

---

## ✅ Benefícios:

1. **Feedback Preciso:** O jogador sabe exatamente quanto de vida resta
2. **Melhor Estratégia:** Fácil calcular quantos tiros faltam
3. **Profissional:** Barras de vida como em jogos AAA

---

## 🐛 Troubleshooting:

### **Texto não aparece:**
- Rode o jogo (o texto é criado no Start)
- Verifique se usou `CriarBarraDeVida` (não adicione `BarraDeVida` manualmente)

### **Texto está cortado ou pequeno demais:**
- Ajuste o tamanho da barra em `CriarBarraDeVida`
- Ou ajuste `Font Size` no componente Text

### **Texto não atualiza:**
- Verifique se o componente `Vida` existe
- Veja o Console para erros

---

## 🚀 Próximo Commit:

Essa atualização já está pronta para commit! Inclui:
- ✅ `BarraDeVida.cs` - Atualizado com suporte a texto
- ✅ `CriarBarraDeVida.cs` - Cria texto automaticamente
- ✅ Documentação atualizada

---

✨ **Agora você tem feedback visual COMPLETO!**

**Formato Final:**
```
      [ 80/100 ]    ← Números exatos
   ████████░░░░     ← Barra visual colorida
```

**Criado em:** 2025-12-29
**Atualização:** Texto de HP adicionado
