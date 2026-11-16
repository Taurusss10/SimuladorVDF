using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 2: el puesto de trabajo y el aula también son dato, así que su coherencia se
    /// puede comprobar sin construir nada.
    ///
    /// Estos tests existen por un fallo concreto: hasta la fase 2 la "mesa" solo era un plano
    /// de colisión invisible para la física de los cables, de modo que los cables se apoyaban
    /// correctamente en el aire y el PLC y el motor flotaban. Nadie lo detectó porque no había
    /// nada que relacionara la altura del plano con la geometría. Ahora sí lo hay, y es esto.
    /// </summary>
    public class VdfEntornoTests
    {
        private VdfPanelLayout _layout;
        private VdfBancoConfig _banco;
        private VdfEntornoConfig _aula;

        /// <summary>
        /// Altura del plano de colisión de la mesa que usa <see cref="VdfColisionadorCable"/>.
        /// Si se cambia allí, hay que cambiarlo aquí: es justo la pareja de valores que se
        /// desincronizó y produjo los cables flotando.
        /// </summary>
        private const float AlturaPlanoMesaFisica = 0.75f;

        [SetUp]
        public void SetUp()
        {
            _layout = VdfPanelLayoutDefaults.Crear();
            _banco = new VdfBancoConfig();
            _aula = new VdfEntornoConfig();
        }

        [TearDown]
        public void TearDown()
        {
            if (_layout != null) Object.DestroyImmediate(_layout);
        }

        // =====================================================================================
        // Coherencia entre lo que se ve y lo que la física calcula
        // =====================================================================================

        [Test]
        public void LaEncimera_EstaALaAlturaDelPlanoDeColisionDeLosCables()
        {
            Assert.AreEqual(AlturaPlanoMesaFisica, _banco.AlturaSuperficie, 0.0001f,
                "La superficie visible de la mesa y el plano contra el que caen los cables " +
                "deben estar a la misma altura. Si no, los cables se apoyan en el aire.");
        }

        // =====================================================================================
        // El banco tiene que poder sostener lo que se le pone encima
        // =====================================================================================

        [Test]
        public void ElBanco_EsMasAnchoQueElTablero()
        {
            Assert.Greater(_banco.Ancho, _layout.Ancho,
                "El tablero no cabe sobre la mesa.");
        }

        [Test]
        public void ElBanco_TieneFondoParaElTableroYSuArmario()
        {
            float fondoNecesario = _layout.Grosor + _banco.FondoArmario;

            Assert.Greater(_banco.Fondo, fondoNecesario,
                "El módulo con su armario sobresale por detrás de la mesa.");
        }

        [Test]
        public void LasPatas_CabenDentroDeLaEncimera()
        {
            float ocupadoX = (_banco.RetranqueoPatas + _banco.LadoPata) * 2f;
            float ocupadoZ = ocupadoX;

            Assert.Less(ocupadoX, _banco.Ancho, "Las patas no caben a lo ancho.");
            Assert.Less(ocupadoZ, _banco.Fondo, "Las patas no caben en el fondo.");
        }

        [Test]
        public void ElEstante_QuedaPorDebajoDeLaEncimera()
        {
            Assert.Less(_banco.AlturaEstante, _banco.AlturaSuperficie - _banco.GrosorEncimera,
                "El estante atraviesa la encimera.");
        }

        // =====================================================================================
        // El aula tiene que contener al banco, y dejar sitio para trabajar
        // =====================================================================================

        [Test]
        public void ElAula_ContieneAlBanco()
        {
            float margenX = (_aula.Ancho - _banco.Ancho) * 0.5f;
            float margenZ = (_aula.Largo - _banco.Fondo) * 0.5f;

            Assert.Greater(margenX, 0.5f, "El banco toca las paredes laterales.");
            Assert.Greater(margenZ, 0.5f, "El banco toca las paredes de fondo o frente.");
        }

        [Test]
        public void ElAula_DejaSitioParaTrabajarDePieDelanteDelTablero()
        {
            // Borde delantero de la mesa, que es donde se pone el alumno.
            float zBordeMesa = _banco.CentroXZ.y - _banco.Fondo * 0.5f;
            float zParedFrente = _aula.CentroXZ.y - _aula.Largo * 0.5f;
            float hueco = zBordeMesa - zParedFrente;

            Assert.Greater(hueco, 1.0f,
                "Entre la mesa y la pared de la espalda no cabe una persona de pie. " +
                "En VR esto se nota de inmediato: el usuario aparece dentro de la pared.");
        }

        [Test]
        public void ElTecho_EstaPorEncimaDeLaCabezaDeUnAdulto()
        {
            Assert.Greater(_aula.AlturaTecho, 2.2f,
                "Techo demasiado bajo; en VR resulta opresivo.");
        }

        // =====================================================================================
        // La ventana no puede invadir las esquinas
        // =====================================================================================

        [Test]
        public void LaVentana_CabeEnLaParedLateral()
        {
            Assert.Less(_aula.AnchoVentana, _aula.Largo - 0.6f,
                "La ventana llega hasta las esquinas del aula.");

            Assert.Less(_aula.AlturaAntepecho + _aula.AltoVentana, _aula.AlturaTecho - 0.1f,
                "La ventana atraviesa el techo.");

            Assert.Greater(_aula.AlturaAntepecho, _aula.AlturaZocalo,
                "El alféizar queda por debajo del zócalo, así que el hueco corta la pintura.");
        }

        // =====================================================================================
        // El armario cierra el tablero sin tapar su cara útil
        // =====================================================================================

        [Test]
        public void ElArmario_CreceHaciaAtrasYNoInvadeLaCaraDelTablero()
        {
            // Convenio: la cara frontal de la placa está en z = 0 y el cuerpo va hacia +Z.
            // El armario debe arrancar en el grosor de la placa o más atrás.
            float zInicioArmario = _layout.Grosor;

            Assert.GreaterOrEqual(zInicioArmario, 0f,
                "El armario arrancaría por delante de la placa y taparía los componentes.");

            Assert.Greater(_banco.FondoArmario, _banco.GrosorChapa * 2f,
                "El armario es más fino que sus propias chapas.");
        }

        // =====================================================================================
        // Los constructores producen geometría, no objetos vacíos
        // =====================================================================================

        [Test]
        public void ConstruirBanco_ProduceEncimeraPatasYEstante()
        {
            var raiz = VdfBancoBuilder.ConstruirBanco(_banco);
            try
            {
                Assert.IsNotNull(raiz);

                var renderers = raiz.GetComponentsInChildren<Renderer>();
                Assert.GreaterOrEqual(renderers.Length, 6,
                    "Faltan piezas: se esperan encimera, faldón, cuatro patas y estante.");

                // Lo importante de verdad: la cara superior tiene que caer en la altura pedida.
                var encimera = raiz.transform.Find("Encimera");
                Assert.IsNotNull(encimera, "No se construyó la encimera.");

                float caraSuperior = encimera.localPosition.y + encimera.localScale.y * 0.5f;
                Assert.AreEqual(_banco.AlturaSuperficie, caraSuperior, 0.0001f,
                    "La cara superior de la encimera no está a la altura de trabajo.");
            }
            finally
            {
                if (raiz != null) Object.DestroyImmediate(raiz);
            }
        }

        [Test]
        public void ConstruirArmario_UsaLasMedidasDelLayout()
        {
            var raiz = VdfBancoBuilder.ConstruirArmario(_layout, _banco);
            try
            {
                Assert.IsNotNull(raiz);

                var trasera = raiz.transform.Find("Trasera");
                Assert.IsNotNull(trasera, "No se construyó la chapa trasera.");

                Assert.AreEqual(_layout.Ancho, trasera.localScale.x, 0.0001f,
                    "La trasera no mide lo mismo que el tablero, así que se vería el canto.");
                Assert.AreEqual(_layout.Alto, trasera.localScale.y, 0.0001f,
                    "La trasera no mide lo mismo que el tablero.");
            }
            finally
            {
                if (raiz != null) Object.DestroyImmediate(raiz);
            }
        }

        [Test]
        public void ConstruirEntorno_CierraElAulaPorLosSeisLados()
        {
            var raiz = VdfEntornoBuilder.Construir(_aula);
            try
            {
                Assert.IsNotNull(raiz);

                Assert.IsNotNull(raiz.transform.Find("Suelo"), "Falta el suelo.");
                Assert.IsNotNull(raiz.transform.Find("Techo"), "Falta el techo.");

                // Suelo y techo deben SOBRESALIR del interior, no medirlo justo: dos
                // superficies que coinciden al milímetro dejan pasar una línea de luz por la
                // junta, que es el fallo que apareció al montar el aula por primera vez.
                var suelo = raiz.transform.Find("Suelo");
                Assert.Greater(suelo.localScale.x, _aula.Ancho,
                    "El suelo mide justo el interior; la junta con la pared dejará pasar luz.");
                Assert.Greater(suelo.localScale.z, _aula.Largo,
                    "El suelo mide justo el interior; la junta con la pared dejará pasar luz.");
            }
            finally
            {
                if (raiz != null) Object.DestroyImmediate(raiz);
            }
        }

        [Test]
        public void ConstruirEntorno_ColocaUnaLuminariaPorCadaPedida()
        {
            var raiz = VdfEntornoBuilder.Construir(_aula);
            try
            {
                int encontradas = 0;
                foreach (var t in raiz.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("Luminaria_")) encontradas++;

                Assert.AreEqual(_aula.NumeroLuminarias, encontradas,
                    "El número de luminarias no coincide con el pedido. La iluminación busca " +
                    "estos objetos por nombre para colocar los focos encima.");
            }
            finally
            {
                if (raiz != null) Object.DestroyImmediate(raiz);
            }
        }
    }
}
