Add-Type -AssemblyName System.Drawing
$src = 'D:\Pic\slime.png'
$dst = 'E:\Game project\ShuYu\Assets\Resources\Sprites\slime_alpha.png'
$bmp = [System.Drawing.Bitmap]::FromFile($src)
$w = $bmp.Width; $h = $bmp.Height
$threshold = 235
for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    $p = $bmp.GetPixel($x, $y)
    if ($p.R -ge $threshold -and $p.G -ge $threshold -and $p.B -ge $threshold) {
      $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $p.R, $p.G, $p.B))
    }
  }
}
$bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("processed -> " + $dst)
