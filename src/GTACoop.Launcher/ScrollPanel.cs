using System.Drawing;
using System.Windows.Forms;

namespace GTACoop.Remake;

/// <summary>
/// Dikey kaydırma çubuğu olan panel. WinForms, odaklanan denetimi görünür
/// alana getirmek için kendiliğinden kaydırır; bu yüzden sayfa açıldığında üst
/// kısım yukarı kayıp kesiliyordu. Bu panel o davranışı kapatır, kaydırmayı
/// yalnızca kullanıcı yapar.
/// </summary>
internal sealed class ScrollPanel : Panel
{
    public ScrollPanel()
    {
        AutoScroll = true;
    }

    protected override Point ScrollToControl(Control activeControl) =>
        DisplayRectangle.Location;
}
