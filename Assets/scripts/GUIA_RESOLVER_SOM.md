# Guia: Som Não Está Saindo no Jogo

## 🎧 Passo a Passo para Resolver

### 1️⃣ **Adicione o Script de Verificação**

1. No Unity, selecione a **Main Camera**
2. Clique em **Add Component**
3. Procure por **"Verificador Audio"**
4. Adicione o script
5. **Rode o jogo** e veja o Console

### 2️⃣ **Verifique o Console**

Procure por mensagens como:

✅ **Se aparecer**:
```
[SomUnidade] 🔊 SOM TOCANDO: nome_do_som | Volume: 0.5 | Loop: True | isPlaying: True
```
→ O sistema está funcionando! O problema pode ser volume ou distância.

❌ **Se aparecer**:
```
❌ NENHUM AUDIOLISTENER NA CENA!
```
→ Vá para o Passo 3

⚠️ **Se aparecer**:
```
Som Motor: NENHUM - SEM SOM!
```
→ Vá para o Passo 4

### 3️⃣ **Adicionar AudioListener** (SE NECESSÁRIO)

Se não tem AudioListener:

1. Selecione a **Main Camera**
2. Clique em **Add Component**
3. Procure por **"Audio Listener"**
4. Adicione
5. **Rode novamente**

### 4️⃣ **Configurar os AudioClips**

Na imagem que você mandou, vejo que **falta adicionar os sons**:

1. Selecione o **Helicóptero** (ou unidade)
2. No componente **Som Unidade**:
   - **Som Motor**: Arraste o arquivo de áudio do helicóptero voando
   - **Som Parado**: (Opcional) Som quando está parado
   - **Som Tiro**: (Opcional) Som ao atirar
   - **Som Explosão**: (Opcional) Som ao morrer

3. Verifique se **Volume Motor** está > 0 (0.5 é bom)

### 5️⃣ **Verificar Configurações 3D**

O som do Unity tem configurações de distância:

1. No componente **Som Unidade**:
   - **Max Distance** (depende do tipo):
     - Helicóptero: 80m
     - Avião: 150m
     - Tanque: 60m
     - Carro: 50m
     - Navio: 100m

2. Se a câmera está **muito longe**, o som não vai tocar!

### 6️⃣ **Teste com Tecla V**

Com o script `VerificadorAudio` na câmera:

1. Rode o jogo
2. Aperte a tecla **V** no teclado
3. Veja o Console para diagnóstico completo

## 🔧 Problemas Comuns

### Problema: "Não escuto nada"

**Causas possíveis**:

| Causa | Solução |
|-------|---------|
| Sem AudioListener | Adicione na Main Camera |
| Sem AudioClip configurado | Arraste os sons no Inspector |
| Volume = 0 | Aumente o Volume Motor |
| Câmera muito longe | Aproxime ou aumente Max Distance |
| Som não é loop | Marque "Loop Motor" = True |

### Problema: "Som toca por 1 segundo e para"

**Causa**: O arquivo de áudio é curto e não está em loop

**Solução**:
1. Verifique se **Loop Motor** está marcado
2. Use um arquivo de áudio de motor **em loop**

### Problema: "Som toca mas muito baixo"

**Solução**:
1. Aumente **Volume Motor** (0.5 → 0.8)
2. Aumente **Volume Global**: Edit → Project Settings → Audio → Global Volume

## 📝 Logs de Debug

Se você ver estes no Console, está funcionando:

```
[SomUnidade] Iniciando som para: Falcon1
   Tipo: Helicoptero
   Som Motor: helicopter_loop
   AudioSource principal: OK
   AudioSource secundário: OK
   Volume Motor: 0.5
   3D Blend: 1
   Max Distance: 80
   AudioListener encontrado. Distância: 25.5m
[SomUnidade] 🔊 SOM TOCANDO: helicopter_loop | Volume: 0.5 | Loop: True | isPlaying: True
```

## 🎯 Checklist Rápido

Marque o que já verificou:

- [ ] Main Camera tem **AudioListener**
- [ ] Som Unidade tem **Som Motor** configurado (AudioClip)
- [ ] **Volume Motor** > 0
- [ ] **Loop Motor** marcado
- [ ] Câmera está **perto da unidade** (< Max Distance)
- [ ] Console mostra **"🔊 SOM TOCANDO"**
- [ ] Volume global do Windows/Unity não está mudo

## 💡 Teste Rápido

Para testar se o sistema está OK:

1. Coloque o helicóptero **perto da câmera** (< 20 metros)
2. Aumente **Volume Motor** para **1.0**
3. Marque **Loop Motor** = True
4. Rode o jogo
5. Aperte **V** no teclado
6. Leia o Console

---

**Ainda não funciona?** Cole os logs do Console aqui que eu te ajudo! 🎧
