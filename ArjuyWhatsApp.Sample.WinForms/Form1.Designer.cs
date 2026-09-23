namespace ArjuyWhatsApp.Sample.WinForms
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabFuncionalidades = new System.Windows.Forms.TabControl();
            this.tabTexto = new System.Windows.Forms.TabPage();
            this.lblNumero = new System.Windows.Forms.Label();
            this.txtNumero = new System.Windows.Forms.TextBox();
            this.lblMensaje = new System.Windows.Forms.Label();
            this.txtMensaje = new System.Windows.Forms.TextBox();
            this.btnEnviar = new System.Windows.Forms.Button();
            this.lblResultado = new System.Windows.Forms.Label();
            this.lblRecibidos = new System.Windows.Forms.Label();
            this.lstMensajes = new System.Windows.Forms.ListBox();
            this.lblEnviados = new System.Windows.Forms.Label();
            this.lstEnviados = new System.Windows.Forms.ListBox();
            this.tabFuncionalidades.SuspendLayout();
            this.tabTexto.SuspendLayout();
            this.SuspendLayout();
            //
            // lblNumero
            //
            this.lblNumero.AutoSize = true;
            this.lblNumero.Location = new System.Drawing.Point(12, 15);
            this.lblNumero.Name = "lblNumero";
            this.lblNumero.Size = new System.Drawing.Size(94, 15);
            this.lblNumero.TabIndex = 0;
            this.lblNumero.Text = "Número destino:";
            //
            // txtNumero
            //
            this.txtNumero.Location = new System.Drawing.Point(140, 12);
            this.txtNumero.Name = "txtNumero";
            this.txtNumero.PlaceholderText = "Ej: 5491122334455";
            this.txtNumero.Size = new System.Drawing.Size(260, 23);
            this.txtNumero.TabIndex = 1;
            //
            // lblMensaje
            //
            this.lblMensaje.AutoSize = true;
            this.lblMensaje.Location = new System.Drawing.Point(12, 47);
            this.lblMensaje.Name = "lblMensaje";
            this.lblMensaje.Size = new System.Drawing.Size(58, 15);
            this.lblMensaje.TabIndex = 2;
            this.lblMensaje.Text = "Mensaje:";
            //
            // txtMensaje
            //
            this.txtMensaje.Location = new System.Drawing.Point(140, 44);
            this.txtMensaje.Name = "txtMensaje";
            this.txtMensaje.Size = new System.Drawing.Size(260, 23);
            this.txtMensaje.TabIndex = 3;
            //
            // btnEnviar
            //
            this.btnEnviar.Location = new System.Drawing.Point(140, 76);
            this.btnEnviar.Name = "btnEnviar";
            this.btnEnviar.Size = new System.Drawing.Size(100, 28);
            this.btnEnviar.TabIndex = 4;
            this.btnEnviar.Text = "Enviar";
            this.btnEnviar.UseVisualStyleBackColor = true;
            this.btnEnviar.Click += new System.EventHandler(this.btnEnviar_Click);
            //
            // lblResultado (dentro de tabTexto)
            //
            this.lblResultado.AutoSize = false;
            this.lblResultado.Location = new System.Drawing.Point(12, 112);
            this.lblResultado.Name = "lblResultado";
            this.lblResultado.Size = new System.Drawing.Size(388, 40);
            this.lblResultado.TabIndex = 5;
            //
            // tabTexto
            //
            this.tabTexto.Controls.Add(this.lblResultado);
            this.tabTexto.Controls.Add(this.btnEnviar);
            this.tabTexto.Controls.Add(this.txtMensaje);
            this.tabTexto.Controls.Add(this.lblMensaje);
            this.tabTexto.Controls.Add(this.txtNumero);
            this.tabTexto.Controls.Add(this.lblNumero);
            this.tabTexto.Location = new System.Drawing.Point(4, 24);
            this.tabTexto.Name = "tabTexto";
            this.tabTexto.Padding = new System.Windows.Forms.Padding(3);
            this.tabTexto.Size = new System.Drawing.Size(420, 220);
            this.tabTexto.TabIndex = 0;
            this.tabTexto.Text = "Texto";
            this.tabTexto.UseVisualStyleBackColor = true;
            //
            // tabFuncionalidades
            //
            // Las demás TabPage (imagen, documento, audio, video, sticker, ubicación, contacto,
            // reacción, plantillas) se agregan por código desde Form1.BuildFeatureTabs() — con esa
            // cantidad de campos por pestaña, armarlas a mano acá con coordenadas absolutas sería
            // imposible de mantener sin el diseñador visual.
            this.tabFuncionalidades.Controls.Add(this.tabTexto);
            this.tabFuncionalidades.Location = new System.Drawing.Point(12, 12);
            this.tabFuncionalidades.Name = "tabFuncionalidades";
            this.tabFuncionalidades.SelectedIndex = 0;
            this.tabFuncionalidades.Size = new System.Drawing.Size(428, 248);
            this.tabFuncionalidades.TabIndex = 10;
            //
            // lblEnviados
            //
            this.lblEnviados.AutoSize = true;
            this.lblEnviados.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblEnviados.Location = new System.Drawing.Point(12, 268);
            this.lblEnviados.Name = "lblEnviados";
            this.lblEnviados.Size = new System.Drawing.Size(160, 15);
            this.lblEnviados.TabIndex = 6;
            this.lblEnviados.Text = "Mensajes enviados:";
            //
            // lstEnviados
            //
            // Muestra, además del texto enviado, el estado de entrega más reciente reportado por
            // MessageStatusUpdated (✓ enviado / ✓✓ entregado / ✓✓ leído / ✗ falló) — ver
            // Form1.OnMessageStatusUpdated. El índice de cada item se trackea en
            // _sentMessageListIndexByMessageId para poder actualizarlo in-place.
            this.lstEnviados.FormattingEnabled = true;
            this.lstEnviados.HorizontalScrollbar = true;
            this.lstEnviados.ItemHeight = 15;
            this.lstEnviados.Location = new System.Drawing.Point(12, 290);
            this.lstEnviados.Name = "lstEnviados";
            this.lstEnviados.Size = new System.Drawing.Size(428, 100);
            this.lstEnviados.TabIndex = 7;
            //
            // lblRecibidos
            //
            this.lblRecibidos.AutoSize = true;
            this.lblRecibidos.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblRecibidos.Location = new System.Drawing.Point(12, 400);
            this.lblRecibidos.Name = "lblRecibidos";
            this.lblRecibidos.Size = new System.Drawing.Size(179, 15);
            this.lblRecibidos.TabIndex = 8;
            this.lblRecibidos.Text = "Mensajes recibidos (webhook):";
            //
            // lstMensajes
            //
            this.lstMensajes.FormattingEnabled = true;
            this.lstMensajes.HorizontalScrollbar = true;
            this.lstMensajes.ItemHeight = 15;
            this.lstMensajes.Location = new System.Drawing.Point(12, 422);
            this.lstMensajes.Name = "lstMensajes";
            this.lstMensajes.Size = new System.Drawing.Size(428, 180);
            this.lstMensajes.TabIndex = 9;
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 614);
            this.Controls.Add(this.lstMensajes);
            this.Controls.Add(this.lblRecibidos);
            this.Controls.Add(this.lstEnviados);
            this.Controls.Add(this.lblEnviados);
            this.Controls.Add(this.tabFuncionalidades);
            this.Name = "Form1";
            this.Text = "ArjuyWhatsApp — Sample WinForms";
            this.tabTexto.ResumeLayout(false);
            this.tabTexto.PerformLayout();
            this.tabFuncionalidades.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TabControl tabFuncionalidades;
        private System.Windows.Forms.TabPage tabTexto;
        private System.Windows.Forms.Label lblNumero;
        private System.Windows.Forms.TextBox txtNumero;
        private System.Windows.Forms.Label lblMensaje;
        private System.Windows.Forms.TextBox txtMensaje;
        private System.Windows.Forms.Button btnEnviar;
        private System.Windows.Forms.Label lblResultado;
        private System.Windows.Forms.Label lblRecibidos;
        private System.Windows.Forms.ListBox lstMensajes;
        private System.Windows.Forms.Label lblEnviados;
        private System.Windows.Forms.ListBox lstEnviados;
    }
}
