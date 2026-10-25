$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$projectRoot=Split-Path -Parent $PSScriptRoot
$items=Get-Content -LiteralPath "$projectRoot\tests\corpus.json" -Raw -Encoding UTF8|ConvertFrom-Json
$synth=New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices=@($synth.GetInstalledVoices()|Where-Object {$_.Enabled}|ForEach-Object {$_.VoiceInfo})
$voices|Select-Object Name,@{n='Culture';e={$_.Culture.Name}}|ConvertTo-Json|Set-Content -LiteralPath "$projectRoot\research\evidence\sapi-voices.json" -Encoding UTF8
$format=New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
New-Item -ItemType Directory -Path "$projectRoot\tests\audio" -Force|Out-Null
foreach($item in $items){
 $voice=$voices|Where-Object {$_.Culture.Name -like ($item.Language+'*')}|Select-Object -First 1
 if(!$voice){throw ('Missing local SAPI voice: '+$item.Language)}
 $synth.SelectVoice($voice.Name)
 $synth.Rate=0
 $synth.SetOutputToWaveFile((Join-Path "$projectRoot\tests" $item.Wav),$format)
 $synth.Speak($item.Text)
 $synth.SetOutputToNull()
}
$synth.Dispose()
Write-Output ('Generated '+$items.Count+' fixed synthetic utterances, 16kHz PCM mono.')
