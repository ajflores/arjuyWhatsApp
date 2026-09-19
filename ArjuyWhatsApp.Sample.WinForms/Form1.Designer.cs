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
            this.lblNumero = new System.Windows.Forms.Label();
            this.txtNumero = new System.Windows.Forms.TextBox();
            this.lblMensaje = new System.Windows.Forms.Label();
            this.txtMensaje = new System.Windows.Forms.TextBox();
            this.btnEnviar = new System.Windows.Forms.Button();
            this.lblResultado = new System.Windows.Forms.Label();
            this.lblRecibidos = new System.Windows.Forms.Label();
            this.lstMensajes = new System.Windows.Forms.ListBox();
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
            this.txtNumero.Size = new System.Drawing.Size(300, 23);
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
            this.txtMensaje.Size = new System.Drawing.Size(300, 23);
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
            // lblResultado
            //
            this.lblResultado.AutoSize = false;
            this.lblResultado.Location = new System.Drawing.Point(12, 112);
            this.lblResultado.Name = "lblResultado";
            this.lblResultado.Size = new System.Drawing.Size(428, 40);
            this.lblResultado.TabIndex = 5;
            //
            // lblRecibidos
            //
            this.lblRecibidos.AutoSize = true;
            this.lblRecibidos.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblRecibidos.Location = new System.Drawing.Point(12, 160);
            this.lblRecibidos.Name = "lblRecibidos";
            this.lblRecibidos.Size = new System.Drawing.Size(179, 15);
            this.lblRecibidos.TabIndex = 6;
            this.lblRecibidos.Text = "Mensajes recibidos (webhook):";
            //
            // lstMensajes
            //
            this.lstMensajes.FormattingEnabled = true;
            this.lstMensajes.HorizontalScrollbar = true;
            this.lstMensajes.ItemHeight = 15;
            this.lstMensajes.Location = new System.Drawing.Point(12, 182);
            this.lstMensajes.Name = "lstMensajes";
            this.lstMensajes.Size = new System.Drawing.Size(428, 244);
            this.lstMensajes.TabIndex = 7;
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 438);
            this.Controls.Add(this.lstMensajes);
            this.Controls.Add(this.lblRecibidos);
            this.Controls.Add(this.lblResultado);
            this.Controls.Add(this.btnEnviar);
            this.Controls.Add(this.txtMensaje);
            this.Controls.Add(this.lblMensaje);
            this.Controls.Add(this.txtNumero);
            this.Controls.Add(this.lblNumero);
            this.Name = "Form1";
            this.Text = "ArjuyWhatsApp — Sample WinForms";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblNumero;
        private System.Windows.Forms.TextBox txtNumero;
        private System.Windows.Forms.Label lblMensaje;
        private System.Windows.Forms.TextBox txtMensaje;
        private System.Windows.Forms.Button btnEnviar;
        private System.Windows.Forms.Label lblResultado;
        private System.Windows.Forms.Label lblRecibidos;
        private System.Windows.Forms.ListBox lstMensajes;
    }
}
