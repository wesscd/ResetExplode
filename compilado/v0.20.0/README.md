# BaraoTechExplode v0.20.0 · Windows x64

**[Baixar a versão compilada](https://github.com/wesscd/ResetExplode/raw/refs/heads/main/compilado/v0.20.0/BaraoTechExplode-v0.20.0-win-x64.zip)**

Extraia o ZIP antes de abrir `BaraoTechExplode.exe`. Feche a versão anterior normalmente para permitir a restauração das sessões temporárias. O pacote contém o .NET necessário, instruções e licença.

Em **Otimização guiada → Lista de otimizações**, confira o item reconhecido, o estado lido e a coluna **Ao aplicar**. Marcar uma caixa apenas coloca a alteração na fila. Selecione uma linha para consultar impactos e motivos de preservação; use **Revisar alterações** para conferir e confirmar a execução. Estados desconhecidos, serviços protegidos e itens sem regra válida são preservados.

O **Console de execução** fica recolhido abaixo da lista e pode ser aberto para acompanhar resultados e falhas. As ferramentas anteriores continuam na aba **Ferramentas avançadas**. A análise é relida após aplicar ou restaurar. A restauração por item exige um backup existente. Os registros são locais em `%AppData%\ResetExplode\logs`, sem envio automático.

Veja a [prévia com dados de demonstração](../../Assets/preview-v0.20.png); ela não representa medições de um computador real.

- Código compilado: [`89a0db604273b9d60e1e2b7224adab18be8e5a2e`](https://github.com/wesscd/ResetExplode/commit/89a0db604273b9d60e1e2b7224adab18be8e5a2e).
- Compilação e testes: [GitHub Actions](https://github.com/wesscd/ResetExplode/actions/runs/40216725266), com 173 verificações automatizadas.
- Tamanho do ZIP: 85.446.252 bytes.
- SHA-256 do ZIP: `d3812d7e0e9283d293d9e58ab91e250a3458dc1d65bc60e1e9e0629a864a140b`.
- SHA-256 do executável: `da5d0c5d52fce0d374e0192e6eb5bb7922c05e83be4baf24d35ca3f48a1492f8`.

Os hashes foram conferidos com os arquivos da release. Esta pasta preserva a v0.20.0, mesmo que a release `latest` passe a apontar para outra versão. Os testes usam backends simulados; não foi aplicada otimização real ao Windows para validar esta publicação.

Código principal: **Gustavo da Reset (GustavoReset)**, autor do [RESET EXPLODE](https://github.com/GustavoReset/ResetExplode). Fork e marca: [Barão Tech](https://baraotech.online/). Licença MIT e copyright original preservados em [LICENSE](LICENSE).
