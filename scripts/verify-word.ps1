param([string]$Root = 'E:\codex手搓程序\bridgevoice-rebuild')
$ErrorActionPreference='Stop'
$word=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
$doc=$word.Documents.Item('文档1')
$text=$doc.Content.Text
$b=Get-Content -Raw -Encoding UTF8 -LiteralPath "$Root\research\evidence\pipeline-word.json"|ConvertFrom-Json
$rows=@($b.Results|ForEach-Object{[pscustomobject]@{id=$_.Id;present=$text.Contains($_.Text)}})
$retained=if(($rows|Where-Object present).Count -gt 0){$text}else{'No corpus marker: text not retained'}
[pscustomobject]@{document=$doc.Name;length=$text.Length;method='Read-only Word COM Content.Text for the previously observed empty research document';matches=$rows;text=$retained}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath "$Root\research\evidence\word-readback.json" -Encoding UTF8
[pscustomobject]@{matches=($rows|Where-Object present).Count;total=$rows.Count}
