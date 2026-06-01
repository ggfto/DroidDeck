
namespace AnyDeck
{
  partial class frmPrincipal
  {
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmPrincipal));
            this.ntiTray = new System.Windows.Forms.NotifyIcon(this.components);
            this.SuspendLayout();
            // 
            // ntiTray
            // 
            this.ntiTray.BalloonTipTitle = "AnyDeck";
            this.ntiTray.Icon = ((System.Drawing.Icon)(resources.GetObject("ntiTray.Icon")));
            this.ntiTray.Text = "AnyDeck";
            // 
            // frmPrincipal
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(545, 254);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "frmPrincipal";
            this.Text = "AnyDeck";
            this.ResumeLayout(false);

        }

    #endregion

    private System.Windows.Forms.NotifyIcon ntiTray;
  }
}

