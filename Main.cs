﻿using SimpleSDK_Demo.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimpleSDK.Helpers;
using SimpleSDK.Enum;

namespace SimpleSDK_Demo
{
    public partial class Main : Form
    {
        Helper handler = new Helper();
        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            handler.Configuracion.LeerArchivo();
        }

        private void botonGenerarDocumento_Click(object sender, EventArgs e)
        {
            GenerarDocumentoElectronico formulario = new GenerarDocumentoElectronico();
            formulario.Show();
        }

        private void botonConfiguracion_Click(object sender, EventArgs e)
        {
            ConfiguracionDemo formulario = new ConfiguracionDemo();
            formulario.Show();
        }

        private async void botonCesion_ClickAsync(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Title = "Seleccione DTE para ser cedido";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                /*La cesión se debe realizar a partir de un DTE existente
                 Para ello, se carga el correspondiente XML.*/
                string pathDTECedido = openFileDialog.FileName;

                /*Datos del factoring*/
                var cesionario = new SimpleSDK.Models.Cesion.Cesionario()
                {
                    Direccion = "Dirección Cesionario",
                    eMail = "Email Cesionario",
                    RazonSocial = "Factoring LTDA",
                    RUT = "11111111-1"
                };

                var cedente = new SimpleSDK.Models.Cesion.Cedente()
                {
                    RUT = "22222222-2",
                    RazonSocial = "Razón Social",
                    Direccion = "Dirección",
                    eMail = "Email",
                    RUTsAutorizados = new List<SimpleSDK.Models.Cesion.RUTAutorizado>()
                    {
                        new SimpleSDK.Models.Cesion.RUTAutorizado()
                        {
                            Nombre = "Nombre Autorizado",
                            RUT = "RUT Autorizado"
                        }
                    }
                };

                var cesion = new SimpleSDK.Models.Cesion.Cesion();
                cesion.Cesionario = cesionario;
                cesion.Cedente = cedente;

                /*Creo el objeto AEC*/
                var AEC = new SimpleSDK.Models.Cesion.AEC();
                AEC.DocumentoAEC.Cesiones.Add(cesion);

                AEC.Certificado.Ruta = handler.Configuracion.Certificado.Ruta;
                AEC.Certificado.Rut = handler.Configuracion.Certificado.Rut;
                AEC.Certificado.Password = handler.Configuracion.Certificado.Password;

                var result = await AEC.GenerarXMLAsync(pathDTECedido, handler.Configuracion.APIKey);

                if (result.Item1)
                {
                    var pathDTE = System.IO.Path.Combine(AppContext.BaseDirectory, $"AEC_{cesionario.RUT}_{DateTime.Now.Ticks}.xml");
                    System.IO.File.WriteAllText(pathDTE, result.Item2, Encoding.GetEncoding("ISO-8859-1"));
                    MessageBox.Show($"Documento AEC generado exitosamente y guardado en {pathDTE}", "Exito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(result.Item2, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }

        private async void botonEnviarSii_Click(object sender, EventArgs e)
        {
            /* Todo envío al SII debe realizarse mediante un XML denominado "Sobre de Envío" o también "EnvioDTE"
             * Un EnvioDTE es un XML que puede albergar hasta 2.000 DTE individuales, para enviarlos todos de forma
             * masiva en un solo archivo.
             * Solamente las boletas deben enviarse mediante otro XML denominado "EnvioBoleta", que cumple con exactamente
             * la misma función que el EnvioDTE. En este caso, soporta un máximo de 500 DTEs de boleta a la vez. Recomendamos 
             * que no sean más de 100.   
             */

            /*************Generación del sobre*************/

            var envioSII = new SimpleSDK.Models.Envios.SobreEnvio();

            /*Estos valores se deben cambiar por los que correspondan*/
            envioSII.Caratula.RutEmisor = handler.Configuracion.Empresa.RutEmpresa;
            envioSII.Caratula.RutReceptor = "60803000-K"; //Si es al SII, debe ir "60803000-K". Si es al cliente, el rut del cliente
            envioSII.Caratula.FechaResolucion = handler.Configuracion.Empresa.FechaResolucion;
            envioSII.Caratula.NumeroResolucion = handler.Configuracion.Empresa.NumeroResolucion;

            /*Datos del certificado*/
            envioSII.Certificado.Ruta = handler.Configuracion.Certificado.Ruta;
            envioSII.Certificado.Rut = handler.Configuracion.Certificado.Rut;
            envioSII.Certificado.Password = handler.Configuracion.Certificado.Password;

            if (!radioRVD.Checked)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = true;
                openFileDialog.Title = "Seleccione DTEs para ser enviados al SII";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string[] pathFiles = openFileDialog.FileNames;
                    var result = await envioSII.GenerarXMLAsync(pathFiles, handler.Configuracion.APIKey);

                    string pathEnvio = string.Empty;
                    if (result.Item1)
                    {
                        pathEnvio = Path.Combine(AppContext.BaseDirectory, $"EnvioDTE_{envioSII.Caratula.RutEmisor}_{pathFiles.Count()}_dtes_{DateTime.Now.Ticks}.xml");
                        File.WriteAllText(pathEnvio, result.Item2, Encoding.GetEncoding("ISO-8859-1"));
                        MessageBox.Show($"Documento EnvioDTE generado exitosamente y guardado en {pathEnvio}", "Exito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(result.Item2, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    /* Una vez teniendo el archivo del sobre, se debe enviar al SII.
                     * Si es un EnvioDTE, irá al maullin en caso de ser de certificación, o al palena en caso de producción
                     * Si es un EnvioBoleta, irá al pangal en caso de ser de certificación, o al rahue en caso de producción                 * 
                     */

                    var resultEnvio = await envioSII.EnviarSIIAsync(pathEnvio, handler.Configuracion.APIKey);
                    if (resultEnvio.Item1)
                    {
                        ResultadoOperacion formulario = new ResultadoOperacion(resultEnvio.Item2.ResponseXml);
                        formulario.ShowDialog();
                    }
                }
            }
            else //Si se trata de un RVD (ex-RCOF)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = false;
                openFileDialog.Title = "Seleccione RCOF para ser enviado al SII";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    /* El archivo de RVD se envía a los mismos servidores que los EnvioDTE. */
                    string pathFile = openFileDialog.FileName;
                    var result = await envioSII.EnviarSIIAsync(pathFile, handler.Configuracion.APIKey);
                    if (result.Item1)
                    {
                        ResultadoOperacion formulario = new ResultadoOperacion(result.Item2.ResponseXml);
                        formulario.ShowDialog();
                    }
                }
            }


            /*************Envío al SII********************/

        }

        private async void botonFacturaCompra_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Title = "Seleccione CAF para generar DTE Factura de Compra";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string pathCAF = openFileDialog.FileName;
                var emisor = new SimpleSDK.Models.DTE.Emisor()
                {
                    Rut = handler.Configuracion.Empresa.RutEmpresa,
                    DireccionOrigen = handler.Configuracion.Empresa.Direccion,
                    ComunaOrigen = handler.Configuracion.Empresa.Comuna,
                    CiudadOrigen = handler.Configuracion.Empresa.Comuna,
                    RazonSocial = handler.Configuracion.Empresa.RazonSocial,
                    Giro = handler.Configuracion.Empresa.Giro,
                    CorreoElectronico = "test@simple-api.cl",
                    Telefono = new List<string>() { "123123123" },
                    ActividadEconomica = handler.Configuracion.Empresa.CodigosActividades.Select(x=>x.Codigo).ToList()
                };

                var receptor = new SimpleSDK.Models.DTE.Receptor()
                {
                    Rut = "17096073-4",
                    RazonSocial = "GONZALO BUSTAMANTE BAÑADOS",
                    Direccion = "DIRECCION 123",
                    Comuna = "COMUNA DE CHILE",
                    Giro = "GIRO DEL RECEPTOR"
                };

                long folio = 1; //Valor debe ser cambiado por el que corresponda
                                 //Creación del objeto
                var dte = new SimpleSDK.Models.DTE.DTE(emisor, receptor, folio, TipoDTE.DTEType.FacturaCompraElectronica);

                //Asignación de detalles
                List<ItemBoleta> items = new List<ItemBoleta>()
            {
                new ItemBoleta(){
                    Afecto = true,
                    Cantidad = 1,
                    Nombre = "ITEM DE PRUEBA",
                    Precio = 5000
                },
                new ItemBoleta(){
                    Afecto = false,
                    Cantidad = 1,
                    Nombre = "EXENTO DE PRUEBA",
                    Precio = 2500
                }
            };
                dte.Documento.Detalles = handler.ItemboletaADetalle(items);

                dte.CalcularTotales();

                dte.Certificado.Ruta = handler.Configuracion.Certificado.Ruta;
                dte.Certificado.Rut = handler.Configuracion.Certificado.Rut;
                dte.Certificado.Password = handler.Configuracion.Certificado.Password;

                try
                {
                    var result = await dte.GenerarXMLAsync(pathCAF, handler.Configuracion.APIKey);
                    if (result.Item1)
                    {
                        var pathDTE = System.IO.Path.Combine(AppContext.BaseDirectory, $"DTE_46_{emisor.Rut}_{folio}.xml");
                        System.IO.File.WriteAllText(pathDTE, result.Item2, Encoding.GetEncoding("ISO-8859-1"));
                        MessageBox.Show($"Documento generado exitosamente y guardado en {pathDTE}", "Exito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(result.Item2, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {

                    MessageBox.Show(ex.Message);
                }
            }

            
        }

        private void botonGenerarNotaCredito_Click(object sender, EventArgs e)
        {
            GenerarNotaCredito formulario = new GenerarNotaCredito();
            formulario.Show();
        }

        private void botonGenerarRCOF_Click(object sender, EventArgs e)
        {
            GenerarRVD formulario = new GenerarRVD();
            formulario.Show();
        }
    }
}
