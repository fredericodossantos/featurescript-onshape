FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");

// =====================================================================
// MESA XY - jogo completo de pecas impressas
//
// Cadeia cinematica (mundo Z-up):
//   base de madeira
//     -> 4x MANCAL_BASE seguram os 2 eixos X (fixos)
//        -> 2x CARRO correm em X sobre 2 LM8UU cada
//           -> 1x SUPORTE_LM8UU parafusado no topo de cada carro
//              -> os 2 eixos Y correm dentro desses suportes
//                 -> 2x MANCAL_MESA nas pontas dos eixos Y
//                    -> mesa de MDF parafusada por baixo
//
// FONTE UNICA DE VERDADE: o padrao de furos que une CARRO e SUPORTE_LM8UU
// sai de cotas() (furoHolderX / furoHolderY) e e consumido pelas DUAS
// pecas - no carro como furo de rosca, no suporte como furo de passagem.
// Nao existe literal repetido: mudar uma cota move os dois furos juntos.
//
// Sintaxe conforme reference/idioma-comprovado.fs (fCylinder, uniao sem
// "targets", numeros puros em mm com a unidade so na fronteira).
// =====================================================================

// -------------------- enums --------------------

annotation { "Name" : "Parafuso" }
export enum ParafusoMesaXy
{
    annotation { "Name" : "M4" }
    M4,
    annotation { "Name" : "M3" }
    M3
}

// -------------------- primitivas (idioma comprovado) --------------------

function makeBlock(context is Context, id is Id, x1 is number, y1 is number, z1 is number,
        x2 is number, y2 is number, z2 is number) returns Query
{
    fCuboid(context, id, {
            "corner1" : vector(x1, y1, z1) * millimeter,
            "corner2" : vector(x2, y2, z2) * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

function cylinder(context is Context, id is Id, a is Vector, b is Vector, radius is number) returns Query
{
    fCylinder(context, id, {
            "bottomCenter" : a * millimeter,
            "topCenter" : b * millimeter,
            "radius" : radius * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

// Cilindro centrado em "center", ao longo de "X", "Y" ou "Z".
function cylinderOnAxis(context is Context, id is Id, center is Vector, axis is string,
        diameter is number, length is number) returns Query
{
    var n;
    if (axis == "X")
    {
        n = vector(1, 0, 0);
    }
    else if (axis == "Y")
    {
        n = vector(0, 1, 0);
    }
    else
    {
        n = vector(0, 0, 1);
    }
    return cylinder(context, id, center - n * (length / 2), center + n * (length / 2), diameter / 2);
}

function combine(context is Context, id is Id, target is Query, additions is array, cuts is array)
{
    if (size(additions) > 0)
    {
        opBoolean(context, id + "join", {
                "tools" : qUnion(concatenateArrays([[target], additions])),
                "operationType" : BooleanOperationType.UNION
        });
    }
    if (size(cuts) > 0)
    {
        opBoolean(context, id + "cut", {
                "targets" : target,
                "tools" : qUnion(cuts),
                "operationType" : BooleanOperationType.SUBTRACTION
        });
    }
}

function nameBody(context is Context, body is Query, label is string)
{
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.NAME,
            "value" : label
    });
}

// PALETA - uma cor por TIPO de peca, definida num lugar so.
// Serve para achar peca na montagem e enxergar o que e impresso, o que e
// cortado e o que e comprado.
function paleta() returns map
{
    return {
        "carro" : color(0.16, 0.45, 0.80),        // azul
        "suporte" : color(0.95, 0.55, 0.12),      // laranja
        "mancalBase" : color(0.85, 0.22, 0.20),   // vermelho
        "mancalMesa" : color(0.20, 0.62, 0.33),   // verde
        "motor" : color(0.55, 0.30, 0.72),        // roxo
        "mancalFuso" : color(0.93, 0.78, 0.12),   // amarelo
        "mdfMesa" : color(0.86, 0.74, 0.52),      // bege claro
        "mdfBase" : color(0.55, 0.42, 0.28),      // marrom
        "aco" : color(0.62, 0.66, 0.70),          // eixos e LM8UU
        "bronze" : color(0.78, 0.58, 0.24),       // castanha
        "aluminio" : color(0.74, 0.76, 0.80),     // polias
        "borracha" : color(0.13, 0.13, 0.15),     // correia
        "motorCorpo" : color(0.28, 0.28, 0.33)    // corpo dos motores
    };
}

// Nome + cor de uma vez: todo corpo passa por aqui.
function nomear(context is Context, body is Query, label is string, cor is Color)
{
    nameBody(context, body, label);
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.APPEARANCE,
            "value" : cor
    });
}

function moveBody(context is Context, id is Id, body is Query, dx is number, dy is number, dz is number)
{
    opTransform(context, id, {
            "bodies" : body,
            "transform" : transform(vector(dx, dy, dz) * millimeter)
    });
}

// -------------------- cotas derivadas: FONTE UNICA --------------------

function diaRosca(p is ParafusoMesaXy) returns number
{
    if (p == ParafusoMesaXy.M3)
    {
        return 2.5;      // furo para rosca direto no plastico
    }
    return 3.3;
}

function diaPassagem(p is ParafusoMesaXy) returns number
{
    if (p == ParafusoMesaXy.M3)
    {
        return 3.4;      // parafuso passa livre
    }
    return 4.5;
}

function cotas(d is map) returns map
{
    var compAloj = d.qtdLm * d.lmL + (d.qtdLm - 1) * d.lmGap;   // 2x24 + 2 = 50
    var dExtAloj = d.lmD + 2 * d.parede;                        // 15 + 6 = 21
    var rosca = diaRosca(d.parafuso);
    var passagem = diaPassagem(d.parafuso);

    // Padrao de furos CARRO <-> SUPORTE_LM8UU (as duas pecas leem daqui).
    var furoHolderX = dExtAloj / 2 + passagem / 2 + d.parede;   // ao lado do alojamento
    var furoHolderY = compAloj / 2 - (passagem / 2 + d.parede); // dentro do comprimento

    return {
        "compAloj" : compAloj,
        "dExtAloj" : dExtAloj,
        "boreLm" : d.lmD + d.folgaLm,
        "boreEixo" : d.eixoD + d.folgaEixoFixo,
        "rosca" : rosca,
        "passagem" : passagem,
        "furoHolderX" : furoHolderX,
        "furoHolderY" : furoHolderY,
        "holderX" : furoHolderX + passagem / 2 + d.parede,      // meia largura do suporte
        "holderZ" : d.parede + d.lmD / 2,                       // altura do centro do eixo Y
        "topoCarro" : d.lmD / 2 + d.parede,                     // topo da placa do carro
        // Quanto o SUPORTE sobe acima do centro do eixo Y. O mancal da mesa
        // precisa ser MAIS alto que isso, senao o MDF encosta no suporte
        // antes de encostar no mancal (interferencia real, conferida no modelo).
        "suporteAcimaEixo" : d.lmD / 2 + d.parede,
        "furoBaseOffset" : 16,                                  // Y do furo no pe do mancal 03
        "mesaAbaixo" : d.eixoD / 2 + d.parede,                  // material sob o eixo Y
        "mesaAcima" : d.lmD / 2 + d.parede + d.folgaMesa        // do eixo Y ate a face do MDF
    };
}

const MARGEM = 1;   // quanto o cilindro de corte estoura a parede que atravessa

// Componentes comerciais - UMA definicao para cada cota de catalogo.
const NEMA17_FLANGE = 42.3;     // face do motor (quadrada)
const NEMA17_PCD = 31;          // distancia entre os 4 furos M3, em quadrado
const NEMA17_FURO = 3.4;        // passagem para M3 no suporte
const NEMA17_CABECA = 5.5;      // cabeca do M3 cilindrico (ISO 4762) que prende o motor
const NEMA17_BOSS = 25;         // folga p/ o ressalto (22) COM barriga de impressao
const CASTANHA_BOSS = 10.2;     // diametro do corpo da castanha T8
const CASTANHA_PCD = 16;        // circulo dos 4 furos da castanha
const CASTANHA_FURO = 3.5;      // furos da castanha (M3)
const ROL608_OD = 22.1;         // 608ZZ: externo 22 + 0,1 de ajuste
const ROL608_W = 7;             // 608ZZ: largura
const ROL608_ENCOSTO = 16;      // furo de encosto atras do rolamento
const PE_MOTOR = 25;            // comprimento do pe do suporte do motor
const PE_MOTOR_ALTURA = 5;      // espessura do pe do suporte do motor
const PE_MOTOR_FURO_Y = 7.5;    // furos do pe em Y=+-7,5: fora dos canais dos parafusos do motor
const FOLGA_CABECA = 0.5;       // folga radial em volta da cabeca do parafuso no canal
const PE_MANCAL_FUSO = 20;      // comprimento do pe do mancal da ponta

// Furo de rosca M4 direto no plastico, fixo (nao depende do parametro
// "parafuso" geral): fixacao do suporte do motor Y (peca 10) no mancal 04.
const MOTOR_Y_ROSCA_M4 = 3.3;

// Furos que prendem o suporte do motor Y (peca 10, etapa futura) no mancal
// 04 - posicoes (dx, dz) em mm relativas ao eixo do fuso Y (fusoYX, zFusoY).
// FONTE UNICA: o mancal 04 fura rosca diametro 3,3 (MOTOR_Y_ROSCA_M4) nesta
// tabela; a peca 10 vai consumir a MESMA tabela com furo de passagem
// diametro 4,5 (M4).
const FUROS_SUPORTE_MOTOR_Y = [
        [-38, -7.5],
        [38, -7.5],
        [-38, 4.5],
        [38, 4.5]
];

// --- Y por fuso: peca 10 (suporte do motor, lado -Y) e peca 11 (mancal do
//     fuso 608, lado +Y). Cotas em Y no MUNDO (mesa centrada). ---
const NEMA17_EIXO = 24;              // comprimento do eixo do motor: da face ate a ponta
const FOLGA_PONTAS_Y = 0.5;          // folga entre a ponta do eixo do motor e a ponta do fuso
const ACOPLADOR_Y_COMP = 25;         // comprimento do acoplador 5x8 entre eixo do motor e fuso
const ACOPLADOR_Y_D = 20;            // diametro externo do acoplador
const SUP_MOTOR_Y_BOSS = 30;         // furo central da placa do motor Y (peca 10) - pedido do usuario
const ESPACO_EXTRA_ACOPLADOR_Y = 5;  // folga a mais para o acoplador: afasta a placa do motor Y
const SUP_MOTOR_Y_ESP = 6;           // espessura do pe e da placa do motor (peca 10)

// Metade da profundidade (Y) do mancal 04 (construirMancalMesa: profund/2).
// 04 e peca existente e nao e mexida aqui - repetida so para achar a face
// externa dela: Y = mesaFuroY + MANCAL_04_MEIA_PROFUND.
const MANCAL_04_MEIA_PROFUND = 10;

// Peca 11: meia largura (X) do corpo e, com o mesmo valor, a "queda" (Z) do
// eixo do fuso ate o fundo do corpo.
const MANCAL_FUSO_Y_MEIO_CORPO = 14;

// Furos verticais que prendem a peca 11 na face de baixo da mesa - X
// relativo ao eixo do fuso (fusoYX); Y sempre = yCentro608 (derivado em
// construirMancalFusoY). A mesa de MDF (etapa 5, nao feita aqui) vai
// consumir a MESMA tabela para os pre-furos.
const FUROS_MANCAL_FUSO_Y_DX = [-18.5, 18.5];

// Encaixe da peca 09 entre os dois suportes 02. A peca 09 entra na JANELA
// natural entre as abas superior e inferior dos 02A/02B: os suportes e que
// a abraçam. A folga e por lado, para a impressao FDM nao travar o encaixe.
const FOLGA_ENCAIXE = 0.1;

// Comprimento do corpo do motor NEMA17 (so a lata, sem o eixo) - mesmo valor
// para o motor do X e o motor do Y (construirReferencias).
const NEMA17_CORPO = 38;

// --- Y por fuso: FONTE UNICA das duas cotas usadas em varios lugares (peca
// 10, peca 11, os moveBody delas, mesa de MDF e construirReferencias). Nao
// recalcular estas somas em outro lugar - chamar estas funcoes.
function yFaceMotorY(d is map) returns number
{
    return d.fusoYPonta - FOLGA_PONTAS_Y - ESPACO_EXTRA_ACOPLADOR_Y - NEMA17_EIXO;   // -248.5
}

function yCentro608Y(d is map) returns number
{
    return d.fusoYPonta + d.fusoYComp - 1.5 - ROL608_W / 2;           // +76 com fuso de 300
}

// =====================================================================
// 1) CARRO - corre em X sobre 2 LM8UU; topo plano com 4 furos roscados
// =====================================================================
function construirCarro(context is Context, id is Id, d is map, c is map, comAba is boolean) returns Query
{
    var topoZ = c.topoCarro;

    // placa de ligacao (corpo alvo): mergulha nos alojamentos, nao so encosta
    var placa = makeBlock(context, id + "placa",
            -c.compAloj / 2, -d.espacoX / 2, topoZ - d.placaEsp,
            c.compAloj / 2, d.espacoX / 2, topoZ);

    // alojamentos dos LM8UU dos eixos X
    var aloj1 = cylinderOnAxis(context, id + "aloj1", vector(0, d.espacoX / 2, 0), "X", c.dExtAloj, c.compAloj);
    var aloj2 = cylinderOnAxis(context, id + "aloj2", vector(0, -d.espacoX / 2, 0), "X", c.dExtAloj, c.compAloj);

    var adicoes = [aloj1, aloj2];
    var cortesAba = [];
    if (comAba)
    {
        var abaInfo = construirAbaCastanha(context, id + "abaCastanha", d, c);
        adicoes = append(adicoes, abaInfo[0]);
        cortesAba = abaInfo[1];
    }

    combine(context, id + "uniao", placa, adicoes, []);

    var cortes = [
            cylinderOnAxis(context, id + "boreLm1", vector(0, d.espacoX / 2, 0), "X", c.boreLm, c.compAloj + 2 * MARGEM),
            cylinderOnAxis(context, id + "boreLm2", vector(0, -d.espacoX / 2, 0), "X", c.boreLm, c.compAloj + 2 * MARGEM)
    ];

    // 4 furos de ROSCA para o suporte de LM8UU (mesmo padrao do suporte)
    var zFuro = topoZ - d.placaEsp / 2;
    var comprimentoFuro = d.placaEsp + 2 * MARGEM;
    var i = 0;
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("roscaHolder" ~ i),
                        vector(sx * c.furoHolderX, sy * c.furoHolderY, zFuro), "Z", c.rosca, comprimentoFuro));
            i += 1;
        }
    }

    cortes = concatenateArrays([cortes, cortesAba]);
    combine(context, id + "furos", placa, [], cortes);
    return placa;
}

// =====================================================================
// 1B) ABA DA CASTANHA - cresce do carro para fora, ate o eixo do fuso.
//
// Fica toda ABAIXO do topo da placa do carro, porque acima passa a mesa.
// Os 4 furos da castanha ficam a 45 graus: assim o furo de cima nao rompe
// a borda da aba. A castanha e redonda, entao basta monta-la girada 45
// graus - os 4 furos dela caem exatamente nestes.
// Devolve [corpo da aba, lista de cortes] para o carro unir e furar.
// =====================================================================
function construirAbaCastanha(context is Context, id is Id, d is map, c is map) returns array
{
    // fuso em coordenada LOCAL do carro (o carro e modelado com o eixo X em Z=0)
    var zFuso = d.fusoZ - d.alturaEixoX;
    var raioFlange = CASTANHA_PCD / 2 + CASTANHA_FURO / 2 + d.parede;

    // Rente a ponta +X do carro: a face da aba fica no mesmo plano da face
    // do carro, que e a face que vai para a mesa de impressao.
    var abaX1 = c.compAloj / 2;
    var abaX0 = abaX1 - d.flangeEsp;

    var aba = makeBlock(context, id + "aba",
            abaX0, d.espacoX / 2 - 10, zFuso - raioFlange,
            abaX1, d.fusoY + raioFlange, c.topoCarro);

    var xFuro = (abaX0 + abaX1) / 2;
    var cortes = [cylinderOnAxis(context, id + "castanhaBoss",
                vector(xFuro, d.fusoY, zFuso), "X", CASTANHA_BOSS + 0.4, d.flangeEsp + 2 * MARGEM)];

    var off = CASTANHA_PCD / 2 * 0.7071;
    var i = 0;
    for (var sy in [-1, 1])
    {
        for (var sz in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("castanhaFuro" ~ i),
                        vector(xFuro, d.fusoY + sy * off, zFuso + sz * off), "X",
                        CASTANHA_FURO, d.flangeEsp + 2 * MARGEM));
            i += 1;
        }
    }
    return [aba, cortes];
}

// =====================================================================
// 5) SUPORTE DO MOTOR NEMA17 - parede vertical + pe na chapa.
//    Imprimir DEITADO sobre a face da parede: o pe cresce para cima e
//    nenhuma face fica em balanco.
// =====================================================================
function construirSuporteMotor(context is Context, id is Id, d is map, c is map) returns Query
{
    var meia = NEMA17_FLANGE / 2 + 1;
    var alturaParede = d.fusoZ + meia;
    var pe = PE_MOTOR;

    // O motor fica do lado +X da parede (eixo entrando pelo furo central), e o
    // corpo dele desce ate Z=0: por isso o pe cresce para -X, senao o pe ficaria
    // debaixo do motor e nao deixaria ele assentar.
    var corpo = makeBlock(context, id + "parede", 0, -meia, 0, d.flangeEsp, meia, alturaParede);
    var base = makeBlock(context, id + "pe", -pe, -meia, 0, 0 + MARGEM, meia, PE_MOTOR_ALTURA);

    // Os 2 parafusos de baixo do motor entram pelo lado -X e ficam quase rente
    // ao pe (fusoZ 21 -> furo em Z 5,5). Um canal ao longo de X, no comprimento
    // todo do pe, deixa passar parafuso, cabeca e chave. Vai do lado interno da
    // cabeca ate a borda do pe (sem deixar tira fina pendurada na impressao
    // deitada). E cortado so no pe, antes da uniao, e estoura as pontas dele:
    // sem face coincidente com a parede, que continua inteira.
    var zFuroBaixo = d.fusoZ - NEMA17_PCD / 2;
    var meiaCanal = NEMA17_CABECA / 2 + FOLGA_CABECA;
    var zFundoCanal = zFuroBaixo - meiaCanal;
    if (zFundoCanal < PE_MOTOR_ALTURA)
    {
        if (zFundoCanal < 1)
        {
            throw regenError("Suporte do motor: com esse fusoZ o canal dos parafusos de baixo atravessa o pe. Aumente fusoZ.");
        }
        if (PE_MOTOR_FURO_Y + c.passagem > NEMA17_PCD / 2 - meiaCanal)
        {
            throw regenError("Suporte do motor: a cabeca do parafuso do pe invade o canal. Reduza PE_MOTOR_FURO_Y.");
        }
        var canais = [];
        for (var sy in [-1, 1])
        {
            canais = append(canais, makeBlock(context, id + ("canal" ~ sy),
                        -pe - MARGEM, sy * (NEMA17_PCD / 2 - meiaCanal), zFundoCanal,
                        2 * MARGEM, sy * (meia + MARGEM), PE_MOTOR_ALTURA + MARGEM));
        }
        opBoolean(context, id + "canais", {
                "targets" : base,
                "tools" : qUnion(canais),
                "operationType" : BooleanOperationType.SUBTRACTION
        });
    }
    combine(context, id + "uniao", corpo, [base], []);

    var cortes = [cylinderOnAxis(context, id + "boss", vector(d.flangeEsp / 2, 0, d.fusoZ), "X",
                NEMA17_BOSS, d.flangeEsp + 2 * MARGEM)];
    var i = 0;
    for (var sy in [-1, 1])
    {
        for (var sz in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("nemaFuro" ~ i),
                        vector(d.flangeEsp / 2, sy * NEMA17_PCD / 2, d.fusoZ + sz * NEMA17_PCD / 2),
                        "X", NEMA17_FURO, d.flangeEsp + 2 * MARGEM));
            i += 1;
        }
    }
    for (var sy in [-1, 1])
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("peFuro" ~ sy),
                    vector(-pe / 2, sy * PE_MOTOR_FURO_Y, PE_MOTOR_ALTURA / 2), "Z",
                    c.passagem, PE_MOTOR_ALTURA + 2 * MARGEM));
    }

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// 6) MANCAL DA PONTA DO FUSO - alojamento do 608ZZ com encosto.
//    Imprimir DEITADO sobre a face da parede: o alojamento sai redondo
//    (eixo do furo na vertical) em vez de virar ponte.
// =====================================================================
function construirMancalFuso(context is Context, id is Id, d is map, c is map) returns Query
{
    var esp = ROL608_W + 2 * d.parede;
    var meia = ROL608_OD / 2 + d.parede;
    // ALTURA LIMITADA DE PROPOSITO: este mancal fica dentro da faixa que o
    // mancal da mesa (04) varre, e o 04 passa com a face de baixo em
    // alturaEixoX + 14. O topo aqui para rasante ao rolamento, sem tampa em
    // cima dele - e o que garante a folga. Nao aumentar.
    var alturaParede = d.fusoZ + ROL608_OD / 2 - 1;   // -1: evita corte TANGENTE (geometria degenerada) e ainda sobra folga
    var pe = PE_MANCAL_FUSO;

    var corpo = makeBlock(context, id + "parede", 0, -meia, 0, esp, meia, alturaParede);
    var base = makeBlock(context, id + "pe", esp, -meia - 4, 0, esp + pe, meia + 4, 5);
    combine(context, id + "uniao", corpo, [base], []);

    var cortes = [
            cylinder(context, id + "alojRol",
                    vector(-MARGEM, 0, d.fusoZ), vector(ROL608_W, 0, d.fusoZ), ROL608_OD / 2),
            cylinderOnAxis(context, id + "encosto", vector(esp / 2, 0, d.fusoZ), "X",
                    ROL608_ENCOSTO, esp + 2 * MARGEM)
    ];
    for (var sy in [-1, 1])
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("peFuro" ~ sy),
                    vector(esp + pe / 2, sy * (meia - 2), 2.5), "Z",
                    c.passagem, 5 + 2 * MARGEM));
    }

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// 7) MESA DE MDF - peca de CORTE, nao de impressao.
//
//    Medidas escolhidas pelo curso da maquina, nao pelo gosto:
//    - em Y ela tem de alcancar os DOIS mancais 04, que ficam nas pontas
//      dos eixos Y (mesaFuroY de cada lado), mais uma folga de borda;
//    - em X ela cobre o papel com sobra, e os 4 furos caem exatamente nos
//      furos de passagem dos 04 (mesmo furoMesaOffset que a peca 04 usa).
//    Os furos aqui sao de PRE-FURO para parafuso no MDF (nao passantes).
// =====================================================================
function construirMesaMdf(context is Context, id is Id, d is map, c is map) returns Query
{
    var corpo = makeBlock(context, id + "chapa",
            -d.mesaX / 2, -d.mesaY / 2, 0,
            d.mesaX / 2, d.mesaY / 2, d.mesaEsp);

    var cortes = [];
    var i = 0;
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("furoMdf" ~ i),
                        vector(sx * d.furoMesaOffset, sy * d.mesaFuroY, d.mesaEsp / 2), "Z",
                        d.preFuroMdf, d.mesaEsp + 2 * MARGEM));
            i += 1;
        }
    }

    // pre-furos do mancal da ponta do fuso Y (peca 11): MESMA tabela que a
    // flange da peca 11 usa (FUROS_MANCAL_FUSO_Y_DX), centrados em
    // yCentro608Y(d) - fonte unica com construirMancalFusoY.
    var iPreFuroFusoY = 0;
    for (var furoDxMdf in FUROS_MANCAL_FUSO_Y_DX)
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("furoMancalFusoY" ~ iPreFuroFusoY),
                    vector(d.fusoYX + furoDxMdf, yCentro608Y(d), d.mesaEsp / 2), "Z",
                    d.preFuroMdf, d.mesaEsp + 2 * MARGEM));
        iPreFuroFusoY += 1;
    }

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// 8) BASE DE MDF - peca de CORTE que serve de GABARITO DE FURACAO.
//
//    Todos os furos vem das MESMAS cotas que geram as pecas impressas:
//    - 8 furos dos 4 mancais 03 (2 cada), em suporteEixoX e espacoX;
//    - 2 furos do pe do suporte do motor (05);
//    - 2 furos do pe do mancal da ponta do fuso (06).
//    Se uma cota mudar, os furos da base andam junto - nao ha numero
//    repetido aqui.
//
//    A chapa nao e simetrica em Y de proposito: o fuso corre numa faixa
//    so de um lado (fusoY), e a chapa acompanha.
// =====================================================================
function construirBaseMdf(context is Context, id is Id, d is map, c is map) returns Query
{
    var x0 = -(d.motorX + 45);
    var x1 = d.motorX + 45;
    var y0 = -(d.espacoX / 2 + 30);
    var y1 = d.fusoY + 30;

    var corpo = makeBlock(context, id + "chapa", x0, y0, -d.baseEsp, x1, y1, 0);

    var cortes = [];
    var i = 0;

    // 4 mancais 03: 2 furos cada
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            for (var sf in [-1, 1])
            {
                cortes = append(cortes, cylinderOnAxis(context, id + ("f03_" ~ i),
                            vector(sx * d.suporteEixoX, sy * d.espacoX / 2 + sf * c.furoBaseOffset, -d.baseEsp / 2),
                            "Z", d.preFuroMdf, d.baseEsp + 2 * MARGEM));
                i += 1;
            }
        }
    }

    // pe do suporte do motor (o pe cresce para -X a partir da parede em motorX)
    var meiaMotor = NEMA17_FLANGE / 2 + 1;
    for (var sy in [-1, 1])
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("f05_" ~ sy),
                    vector(d.motorX - PE_MOTOR / 2, d.fusoY + sy * (meiaMotor - 6), -d.baseEsp / 2),
                    "Z", d.preFuroMdf, d.baseEsp + 2 * MARGEM));
    }

    // pe do mancal da ponta do fuso (pe cresce para +X a partir da parede em mancalFusoX)
    var espRol = ROL608_W + 2 * d.parede;
    var meiaRol = ROL608_OD / 2 + d.parede;
    for (var sy in [-1, 1])
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("f06_" ~ sy),
                    vector(d.mancalFusoX + espRol + PE_MANCAL_FUSO / 2, d.fusoY + sy * (meiaRol - 2), -d.baseEsp / 2),
                    "Z", d.preFuroMdf, d.baseEsp + 2 * MARGEM));
    }

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// R) CORPOS DE REFERENCIA - comprados, NAO imprimir.
//    Ficam direto nas coordenadas da maquina (nao no layout), para
//    poderem ser inseridos na montagem sem nenhuma transformacao.
//    Servem para ver folga, curso e batida - nada mais.
// =====================================================================
function construirReferencias(context is Context, id is Id, d is map, c is map)
{
    var pal = paleta();
    var zEixoY = d.alturaEixoX + c.topoCarro + c.holderZ;      // 20 + 10,5 + 10,5
    var passoLm = (d.lmL + d.lmGap) / 2;                       // 13: meio a meio dos 2 LM8UU
    var i = 0;

    // 2 eixos X
    for (var sy in [-1, 1])
    {
        var e = cylinderOnAxis(context, id + ("eixoX" ~ sy), vector(0, sy * d.espacoX / 2, d.alturaEixoX),
                "X", d.eixoD, d.eixoComp);
        nomear(context, e, "REF_eixo_X_" ~ (sy > 0 ? "A" : "B") ~ "_NAO_IMPRIMIR", pal.aco);
    }

    // 2 eixos Y
    for (var sx in [-1, 1])
    {
        var e2 = cylinderOnAxis(context, id + ("eixoY" ~ sx), vector(sx * d.espacoY / 2, 0, zEixoY),
                "Y", d.eixoD, d.eixoComp);
        nomear(context, e2, "REF_eixo_Y_" ~ (sx > 0 ? "A" : "B") ~ "_NAO_IMPRIMIR", pal.aco);
    }

    // LM8UU dos carros: 2 por eixo, em cada um dos 2 carros
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            for (var sl in [-1, 1])
            {
                var b = cylinderOnAxis(context, id + ("lmCarro" ~ i),
                        vector(sx * d.espacoY / 2 + sl * passoLm, sy * d.espacoX / 2, d.alturaEixoX),
                        "X", d.lmD, d.lmL);
                nomear(context, b, "REF_LM8UU_" ~ i ~ "_NAO_IMPRIMIR", pal.aco);
                i += 1;
            }
        }
    }

    // LM8UU dos suportes (eixo Y)
    for (var sx2 in [-1, 1])
    {
        for (var sl2 in [-1, 1])
        {
            var b2 = cylinderOnAxis(context, id + ("lmSup" ~ i),
                    vector(sx2 * d.espacoY / 2, sl2 * passoLm, zEixoY), "Y", d.lmD, d.lmL);
            nomear(context, b2, "REF_LM8UU_" ~ i ~ "_NAO_IMPRIMIR", pal.aco);
            i += 1;
        }
    }

    // fuso TR8: ponta no acoplador, junto ao motor
    var xFusoPonta = d.motorX - 25;
    var fuso = cylinder(context, id + "fuso",
            vector(xFusoPonta - d.fusoComp, d.fusoY, d.fusoZ) * 1,
            vector(xFusoPonta, d.fusoY, d.fusoZ) * 1, d.eixoD / 2);
    nomear(context, fuso, "REF_fuso_TR8_NAO_IMPRIMIR", pal.aco);

    // castanha de bronze, encostada na face externa da aba do carro
    var xAba = d.espacoY / 2 + c.compAloj / 2;
    var flange = cylinder(context, id + "castFlange",
            vector(xAba, d.fusoY, d.fusoZ) * 1, vector(xAba + 3.5, d.fusoY, d.fusoZ) * 1, 11);
    var boss = cylinder(context, id + "castBoss",
            vector(xAba + 3.5, d.fusoY, d.fusoZ) * 1, vector(xAba + 15, d.fusoY, d.fusoZ) * 1,
            CASTANHA_BOSS / 2);
    combine(context, id + "castanha", flange, [boss], []);
    nomear(context, flange, "REF_castanha_TR8_NAO_IMPRIMIR", pal.bronze);

    // ---------------- fuso Y: TR8, castanha, acoplador, motor, rolamento ----
    // Representacao so para ver folga e posicao, ao longo do eixo do fuso Y
    // (eixo em X = d.fusoYX, Z = d.fusoYZ).
    var fusoYTr8 = cylinder(context, id + "fusoYTr8",
            vector(d.fusoYX, d.fusoYPonta, d.fusoYZ) * 1,
            vector(d.fusoYX, d.fusoYPonta + d.fusoYComp, d.fusoYZ) * 1, d.eixoD / 2);
    nomear(context, fusoYTr8, "REF_fuso_Y_TR8_NAO_IMPRIMIR", pal.aco);

    // castanha Y: mesma forma da castanha do X (flange raio 11 x 3,5 + corpo
    // CASTANHA_BOSS) - flange encostada na face +Y da parede de 6 mm da
    // ponte 09, corpo atravessando a parede para -Y.
    var yParedePonteY = 3;    // face +Y da parede de 6 mm da ponte 09
    var castFlangeY = cylinder(context, id + "castFlangeY",
            vector(d.fusoYX, yParedePonteY, d.fusoYZ) * 1,
            vector(d.fusoYX, yParedePonteY + 3.5, d.fusoYZ) * 1, 11);
    var castBossY = cylinder(context, id + "castBossY",
            vector(d.fusoYX, yParedePonteY, d.fusoYZ) * 1,
            vector(d.fusoYX, yParedePonteY - 11.5, d.fusoYZ) * 1, CASTANHA_BOSS / 2);
    combine(context, id + "castanhaY", castFlangeY, [castBossY], []);
    nomear(context, castFlangeY, "REF_castanha_Y_NAO_IMPRIMIR", pal.bronze);

    // acoplador 5x8 entre a ponta do eixo do motor e a ponta do fuso
    var acopladorY = cylinderOnAxis(context, id + "acopladorY",
            vector(d.fusoYX, (yFaceMotorY(d) + NEMA17_EIXO + d.fusoYPonta) / 2, d.fusoYZ), "Y",
            ACOPLADOR_Y_D, ACOPLADOR_Y_COMP);
    nomear(context, acopladorY, "REF_acoplador_Y_NAO_IMPRIMIR", pal.aluminio);

    // motor NEMA17 do Y: deitado, eixo apontando para +Y (para o acoplador)
    var motorYCorpo = makeBlock(context, id + "motorYCorpo",
            d.fusoYX - NEMA17_FLANGE / 2, yFaceMotorY(d) - NEMA17_CORPO, d.fusoYZ - NEMA17_FLANGE / 2,
            d.fusoYX + NEMA17_FLANGE / 2, yFaceMotorY(d), d.fusoYZ + NEMA17_FLANGE / 2);
    nomear(context, motorYCorpo, "REF_motor_NEMA17_Y_NAO_IMPRIMIR", pal.motorCorpo);

    // eixo do motor Y: da face do motor ate a ponta (onde comeca o acoplador)
    var eixoMotorY = cylinder(context, id + "eixoMotorY",
            vector(d.fusoYX, yFaceMotorY(d), d.fusoYZ) * 1,
            vector(d.fusoYX, yFaceMotorY(d) + NEMA17_EIXO, d.fusoYZ) * 1, 2.5);    // diametro 5
    nomear(context, eixoMotorY, "REF_eixo_motor_Y_NAO_IMPRIMIR", pal.aco);

    // rolamento 608 na ponta +Y do fuso, dentro do mancal 11
    var rolamento608Y = cylinderOnAxis(context, id + "rolamento608Y",
            vector(d.fusoYX, yCentro608Y(d), d.fusoYZ), "Y", 22, ROL608_W);
    nomear(context, rolamento608Y, "REF_rolamento_608_Y_NAO_IMPRIMIR", pal.aco);

    // motor do X: atras da parede do suporte 05
    var motorX = makeBlock(context, id + "motorX",
            d.motorX + d.flangeEsp, d.fusoY - NEMA17_FLANGE / 2, d.fusoZ - NEMA17_FLANGE / 2,
            d.motorX + d.flangeEsp + NEMA17_CORPO, d.fusoY + NEMA17_FLANGE / 2, d.fusoZ + NEMA17_FLANGE / 2);
    nomear(context, motorX, "REF_motor_NEMA17_X_NAO_IMPRIMIR", pal.motorCorpo);

    // acoplador entre o motor do X e o fuso
    var acoplador = cylinder(context, id + "acoplador",
            vector(xFusoPonta, d.fusoY, d.fusoZ) * 1,
            vector(d.motorX + d.flangeEsp, d.fusoY, d.fusoZ) * 1, 10);
    nomear(context, acoplador, "REF_acoplador_NAO_IMPRIMIR", pal.aluminio);
}


// =====================================================================
// 09) BERCO DA CASTANHA DO FUSO Y
//
//     ENCAIXADA entre os dois suportes 02 (sem parafuso, sem furar o
//     02A): as pontas entram nas janelas entre as abas superior e inferior
//     dos 02A/02B. Portanto são os suportes que abraçam a peça 09.
//
//     A viga central e a parede do berco: o fuso Y atravessa o furo
//     central (diametro da castanha) e a castanha fica presa nela pelos
//     4 furos M3 a 45 graus, no mesmo padrao usado na aba do carro 01B
//     para a castanha do X (CASTANHA_PCD / CASTANHA_FURO).
// =====================================================================
function construirPonteY(context is Context, id is Id, d is map, c is map) returns Query
{
    var zCarro = d.alturaEixoX + c.topoCarro;          // topo do carro: 30,5
    var xFace = d.espacoY / 2 - c.holderX;             // face interna do 02A: 29
    // Topo da peca (viga e placas de ponta): 2,6 mm abaixo da mesa
    // (Z=60,5 no mundo); acima da flange da castanha nao precisa ir.
    var zTopo = d.fusoYZ + 10.4;                       // 47,5 + 10,4 = 57,9
    // Fundo da viga central (parede do berco).
    var zFundoViga = zCarro;                           // 30,5: fundo plano com placas -> imprime em pe sem suporte

    // viga central: parede do berco, 6 mm em Y, no eixo do fuso em Z
    var vigaY = 3;                                      // meia largura: 6 mm ao todo
    var viga = makeBlock(context, id + "viga",
            -xFace, -vigaY, zFundoViga,
            xFace, vigaY, zTopo);

    // ENCAIXE CORRETO: estas duas linguas ocupam somente a janela lateral
    // que ja existe ENTRE as abas superior e inferior. Nao ha lingueta sobre
    // as abas e tampouco rebaixo nelas: as duas abas dos suportes 02 é que
    // guiam e abraçam cada ponta da peca 09.
    var profundidadeJanela = profundidadeJanelaEncaixeX(d, c);
    var meiaJanelaY = meiaJanelaEncaixeY(d, c);
    var adicoes = [];
    for (var sx in [-1, 1])
    {
        // Sobrepoe 0,1 mm a viga para a uniao booleana; a ponta para antes
        // da parede externa da janela, mantendo a mesma folga de impressao.
        adicoes = append(adicoes, makeBlock(context, id + ("linguaJanela" ~ sx),
                    min(sx * (xFace - FOLGA_ENCAIXE),
                        sx * (xFace + profundidadeJanela - FOLGA_ENCAIXE)),
                    -(meiaJanelaY - FOLGA_ENCAIXE), zCarro,
                    max(sx * (xFace - FOLGA_ENCAIXE),
                        sx * (xFace + profundidadeJanela - FOLGA_ENCAIXE)),
                    meiaJanelaY - FOLGA_ENCAIXE, zTopo));
    }
    combine(context, id + "uniao", viga, adicoes, []);

    var cortes = [];

    // furo central da castanha (boss) e os 4 furos M3 a 45 graus, todos
    // ao longo de Y, centrados no eixo do fuso (fusoYX, fusoYZ) e
    // passantes na viga (mesmo padrao de construirAbaCastanha, so que
    // com o furo ao longo de Y em vez de X).
    var comprimentoFuroY = 2 * vigaY + 2 * MARGEM;      // atravessa os 6 mm da viga + folga
    cortes = append(cortes, cylinderOnAxis(context, id + "castanhaBossY",
                vector(d.fusoYX, 0, d.fusoYZ), "Y", CASTANHA_BOSS + 0.4, comprimentoFuroY));

    var off = CASTANHA_PCD / 2 * 0.7071;
    var iCast = 0;
    for (var sx3 in [-1, 1])
    {
        for (var sz in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("castanhaFuroY" ~ iCast),
                        vector(d.fusoYX + sx3 * off, 0, d.fusoYZ + sz * off), "Y",
                        CASTANHA_FURO, comprimentoFuroY));
            iCast += 1;
        }
    }

    combine(context, id + "furos", viga, [], cortes);
    return viga;
}

// =====================================================================
// 10) SUPORTE DO MOTOR Y - aparafusado na face externa do mancal 04, lado
//     -Y (Y = -200). Pe fixo no 04, duas paredes em balanco ate a placa
//     do motor; o meio fica aberto (acoplador + chave dos parafusos dele).
//     Modelada direto em coordenadas do MUNDO (mesa centrada): ao contrario
//     do 04, esta peca so existe de um lado, sem espelho.
// =====================================================================
function construirSuporteMotorY(context is Context, id is Id, d is map, c is map) returns Query
{
    // Mesmas somas que construirMancalMesa ja usa (fundo do 04 / face de
    // baixo da mesa) - FONTE UNICA, nao redigitar as alturas.
    var zFundoMesa = d.alturaEixoX + c.topoCarro + c.holderZ + c.mesaAcima;      // 60.5
    var zFundoMancal04 = zFundoMesa - (c.mesaAbaixo + c.mesaAcima);              // 34
    var zTopoPe = zFundoMesa - 2;                                                // 58.5 - a mesa ainda esta em 60,5 nesta faixa de Y

    // face externa do mancal 04 do lado -Y (o furo M4 do 04 usa a mesma soma)
    var yFace04 = -(d.mesaFuroY + MANCAL_04_MEIA_PROFUND);                       // -200

    // ponta do eixo do motor e faixa do acoplador - fonte unica fusoYPonta/
    // FOLGA_PONTAS_Y/NEMA17_EIXO/ACOPLADOR_Y_COMP
    var yFaceMotor = yFaceMotorY(d);                                             // -248.5
    var yPontaEixoMotor = yFaceMotor + NEMA17_EIXO;                              // -224.5
    var yMeioAcopla = (yPontaEixoMotor + d.fusoYPonta) / 2;                      // -221.75: acoplador centrado no vao
    var acoplaY0 = yMeioAcopla - ACOPLADOR_Y_COMP / 2;                           // -234.25
    var acoplaY1 = yMeioAcopla + ACOPLADOR_Y_COMP / 2;                           // -209.25

    var peInnerY = yFace04 - SUP_MOTOR_Y_ESP;                                    // -206
    var placaInnerY = yFaceMotor + SUP_MOTOR_Y_ESP;                              // -242.5

    if ((peInnerY - acoplaY1) < 1)
    {
        throw regenError("Suporte do motor Y: o acoplador encosta no pe (menos de 1 mm de folga). Confira fusoYPonta/FOLGA_PONTAS_Y.");
    }
    if ((acoplaY0 - placaInnerY) < 1)
    {
        throw regenError("Suporte do motor Y: a placa do motor invade o acoplador. Confira NEMA17_EIXO/ACOPLADOR_Y_COMP.");
    }

    var meiaPe = 44;                 // cobre os 4 furos M4 em X=+-38 (FUROS_SUPORTE_MOTOR_Y) com folga de borda
    var meiaPlacaMotor = 25;         // meia largura da placa do motor e face externa do braco
    var meiaBracoInterno = 20.5;     // vao livre do braco para o acoplador + chave

    if (meiaBracoInterno <= ACOPLADOR_Y_D / 2)
    {
        throw regenError("Suporte do motor Y: vao do braco menor que o acoplador.");
    }

    var pe = makeBlock(context, id + "pe",
            -meiaPe, peInnerY, zFundoMancal04,
            meiaPe, yFace04, zTopoPe);

    var bracoA = makeBlock(context, id + "bracoA",
            meiaBracoInterno, placaInnerY, zFundoMancal04,
            meiaPlacaMotor, peInnerY, zTopoPe);
    var bracoB = makeBlock(context, id + "bracoB",
            -meiaPlacaMotor, placaInnerY, zFundoMancal04,
            -meiaBracoInterno, peInnerY, zTopoPe);

    var meiaFlangeMotor = NEMA17_FLANGE / 2;
    var placa = makeBlock(context, id + "placa",
            -meiaPlacaMotor, yFaceMotor, d.fusoYZ - meiaFlangeMotor,
            meiaPlacaMotor, placaInnerY, d.fusoYZ + meiaFlangeMotor);

    combine(context, id + "uniao", pe, [bracoA, bracoB, placa], []);

    var yMeioPe = (peInnerY + yFace04) / 2;
    var compFuroPe = SUP_MOTOR_Y_ESP + 2 * MARGEM;
    var cortes = [
            // passagem do fuso - mesmo diametro 10 do furoFusoY do mancal 04
            cylinderOnAxis(context, id + "furoFusoPe", vector(d.fusoYX, yMeioPe, d.fusoYZ), "Y", 10, compFuroPe)
    ];

    // 4 furos de passagem M4, MESMA tabela que o mancal 04 usa para a rosca
    var iSup = 0;
    for (var furo in FUROS_SUPORTE_MOTOR_Y)
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("furoM4_" ~ iSup),
                    vector(d.fusoYX + furo[0], yMeioPe, d.fusoYZ + furo[1]), "Y",
                    diaPassagem(ParafusoMesaXy.M4), compFuroPe));
        iSup += 1;
    }

    // furo central e 4 furos do NEMA17 na placa do motor - mesmo padrao de
    // construirSuporteMotor (X), so que ao longo de Y
    var yMeioPlaca = (yFaceMotor + placaInnerY) / 2;
    var compFuroPlaca = SUP_MOTOR_Y_ESP + 2 * MARGEM;
    cortes = append(cortes, cylinderOnAxis(context, id + "motorBoss",
                vector(d.fusoYX, yMeioPlaca, d.fusoYZ), "Y", SUP_MOTOR_Y_BOSS, compFuroPlaca));
    var iNema = 0;
    for (var sx in [-1, 1])
    {
        for (var sz in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("nemaFuroY" ~ iNema),
                        vector(d.fusoYX + sx * NEMA17_PCD / 2, yMeioPlaca, d.fusoYZ + sz * NEMA17_PCD / 2),
                        "Y", NEMA17_FURO, compFuroPlaca));
            iNema += 1;
        }
    }

    combine(context, id + "furos", pe, [], cortes);
    return pe;
}

// =====================================================================
// 11) MANCAL DO FUSO Y (608ZZ) - pendurado na face de baixo da mesa, lado
//     +Y, na ponta do fuso. Flange aparafusada por baixo do MDF; alojamento
//     do rolamento aberto para +Y (o rolamento entra por ali). Modelada
//     direto em coordenadas do MUNDO, como a peca 10.
// =====================================================================
function construirMancalFusoY(context is Context, id is Id, d is map, c is map) returns Query
{
    var zFundoMesa = d.alturaEixoX + c.topoCarro + c.holderZ + c.mesaAcima;   // 60.5 - mesma soma da peca 10 / construirMancalMesa

    var yPonta608 = d.fusoYPonta + d.fusoYComp;              // +81 - ponta do fuso do lado do 608 (fuso de 300)
    var encostoEsp = 3;                                       // espessura do encosto do furo do fuso
    var rolY0 = yPonta608 - 1.5 - ROL608_W;                   // 122.5
    var rolY1 = yPonta608 - 1.5;                              // 129.5 - o fuso passa 1,5 mm do rolamento
    var yCentro608 = yCentro608Y(d);                          // 126

    var corpoY0 = rolY0 - encostoEsp;                         // 119.5
    var corpoY1 = rolY1;                                      // 129.5 - face +Y do corpo, flush com o rolamento

    var flangeEsp2 = 8;                                       // espessura da flange (peca 11 - independente de d.flangeEsp, outra peca)
    var meiaFlange = 23;                                      // meia largura (X) da flange
    var meioFlangeY = 12;                                     // meio comprimento (Y) da flange, cada lado de yCentro608

    var flangeZ1 = zFundoMesa;                                // 60.5 - face de cima, colada no MDF
    var flangeZ0 = zFundoMesa - flangeEsp2;                   // 52.5
    var corpoZ0 = d.fusoYZ - MANCAL_FUSO_Y_MEIO_CORPO;        // 33.5
    var corpoZ1 = flangeZ0;                                   // 52.5 - topo do corpo = fundo da flange

    var flange = makeBlock(context, id + "flange",
            -meiaFlange, yCentro608 - meioFlangeY, flangeZ0,
            meiaFlange, yCentro608 + meioFlangeY, flangeZ1);

    var corpo = makeBlock(context, id + "corpo",
            -MANCAL_FUSO_Y_MEIO_CORPO, corpoY0, corpoZ0,
            MANCAL_FUSO_Y_MEIO_CORPO, corpoY1, corpoZ1);

    combine(context, id + "uniao", flange, [corpo], []);

    var cortes = [];

    // furos verticais da flange - MESMA tabela que a mesa de MDF (etapa 5,
    // nao feita aqui) vai consumir para os pre-furos.
    var iFl = 0;
    for (var furoDx in FUROS_MANCAL_FUSO_Y_DX)
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("furoFlange" ~ iFl),
                    vector(d.fusoYX + furoDx, yCentro608, (flangeZ0 + flangeZ1) / 2), "Z",
                    diaPassagem(ParafusoMesaXy.M4), flangeEsp2 + 2 * MARGEM));
        iFl += 1;
    }

    // alojamento do 608: aberto para +Y (estoura a face +Y do corpo em MARGEM)
    cortes = append(cortes, cylinder(context, id + "alojRol608",
                vector(d.fusoYX, rolY0, d.fusoYZ), vector(d.fusoYX, rolY1 + MARGEM, d.fusoYZ), ROL608_OD / 2));

    // encosto: furo passante diametro 16 para o fuso, dentro da espessura do encosto
    cortes = append(cortes, cylinderOnAxis(context, id + "furoEncosto608",
                vector(d.fusoYX, (corpoY0 + rolY0) / 2, d.fusoYZ), "Y",
                ROL608_ENCOSTO, encostoEsp + 2 * MARGEM));

    combine(context, id + "furos", flange, [], cortes);
    return flange;
}

// =====================================================================
// 2) SUPORTE_LM8UU em DUAS metades (02A base + 02B tampa)
//
//    Grampo: as duas metades apertam os 2 LM8UU do eixo Y. A canaleta das
//    duas e um semicilindro de raio lmD/2 CENTRADO no mesmo Z, e entre as
//    faces de partida fica um vao (gapAperto) - e ele que deixa o parafuso
//    apertar de verdade em vez de as metades se encostarem antes.
//
//    Material: o corpo central tem so a largura do alojamento (dExtAloj);
//    a largura cheia existe apenas nas 4 abas dos parafusos (padY de
//    comprimento), que e onde o parafuso passa.
//
//    Impressao: cada metade vai para a mesa com a FACE PLANA EXTERNA para
//    baixo e a canaleta para cima (a tampa entra virada). Assim a cavidade
//    so abre conforme sobe - nenhuma face em balanco, zero suporte.
//
//    Um unico parafuso por canto atravessa tampa + base e rosqueia no carro.
// =====================================================================
function abasSuporte(context is Context, id is Id, d is map, c is map,
        z0 is number, z1 is number) returns array
{
    var abas = [];
    var i = 0;
    // A janela de encaixe da 09 fica exatamente entre estas duas abas em Y.
    // As duas funcoes abaixo sao a fonte unica das faces internas da janela.
    var meiaJanelaY = meiaJanelaEncaixeY(d, c);
    var inicioAbaX = c.holderX - profundidadeJanelaEncaixeX(d, c);
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            var x0 = sx * inicioAbaX;
            var x1 = sx * c.holderX;
            abas = append(abas, makeBlock(context, id + ("aba" ~ i),
                        min(x0, x1), min(sy * meiaJanelaY, sy * (meiaJanelaY + d.padY)), z0,
                        max(x0, x1), max(sy * meiaJanelaY, sy * (meiaJanelaY + d.padY)), z1));
            i += 1;
        }
    }
    return abas;
}

// Faces livres da janela rosa: a peca 09 usa estas mesmas cotas para que
// entre entre os bracos dos suportes, sem criar qualquer encaixe nas abas.
function meiaJanelaEncaixeY(d is map, c is map) returns number
{
    return c.furoHolderY - d.padY / 2;
}

function profundidadeJanelaEncaixeX(d is map, c is map) returns number
{
    return c.holderX - c.dExtAloj / 2;
}

function furosSuporte(context is Context, id is Id, d is map, c is map,
        z0 is number, z1 is number) returns array
{
    var furos = [];
    var i = 0;
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            furos = append(furos, cylinderOnAxis(context, id + ("furoFix" ~ i),
                        vector(sx * c.furoHolderX, sy * c.furoHolderY, (z0 + z1) / 2), "Z",
                        c.passagem, (z1 - z0) + 2 * MARGEM));
            i += 1;
        }
    }
    return furos;
}

// 02A - metade de baixo: assenta no carro, canaleta para cima
function construirSuporteBase(context is Context, id is Id, d is map, c is map) returns Query
{
    var zPartida = c.holderZ - d.gapAperto / 2;

    var corpo = makeBlock(context, id + "corpo",
            -c.dExtAloj / 2, -c.compAloj / 2, 0,
            c.dExtAloj / 2, c.compAloj / 2, zPartida);

    combine(context, id + "uniao", corpo, abasSuporte(context, id, d, c, 0, zPartida), []);

    var cortes = [cylinderOnAxis(context, id + "canaleta", vector(0, 0, c.holderZ), "Y",
                d.lmD, c.compAloj + 2 * MARGEM)];
    cortes = concatenateArrays([cortes, furosSuporte(context, id, d, c, 0, zPartida)]);

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// 02B - metade de cima: face plana externa em Z=topo, canaleta para baixo
function construirSuporteTampa(context is Context, id is Id, d is map, c is map) returns Query
{
    var zPartida = c.holderZ + d.gapAperto / 2;
    var topo = c.holderZ + d.lmD / 2 + d.parede;      // 10.5 + 7.5 + 3 = 21

    var corpo = makeBlock(context, id + "corpo",
            -c.dExtAloj / 2, -c.compAloj / 2, zPartida,
            c.dExtAloj / 2, c.compAloj / 2, topo);

    combine(context, id + "uniao", corpo, abasSuporte(context, id, d, c, zPartida, topo), []);

    var cortes = [cylinderOnAxis(context, id + "canaleta", vector(0, 0, c.holderZ), "Y",
                d.lmD, c.compAloj + 2 * MARGEM)];
    cortes = concatenateArrays([cortes, furosSuporte(context, id, d, c, zPartida, topo)]);

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// 3) MANCAL_BASE - segura a ponta de um eixo X; parafusado na base
// =====================================================================
function construirMancalBase(context is Context, id is Id, d is map, c is map) returns Query
{
    var meiaEspParede = 5;
    var meiaLargParede = d.eixoD / 2 + d.parede;                       // 4 + 3 = 7
    var alturaPe = 5;
    var meiaLargPeX = 10;
    var offsetFuroPe = c.furoBaseOffset;
    var meiaLargPeY = offsetFuroPe + c.passagem / 2 + d.parede;        // 16 + 2.25 + 3
    var topoZ = d.alturaEixoX + d.eixoD / 2 + d.parede;

    var parede = makeBlock(context, id + "parede",
            -meiaEspParede, -meiaLargParede, 0,
            meiaEspParede, meiaLargParede, topoZ);

    var pe = makeBlock(context, id + "pe",
            -meiaLargPeX, -meiaLargPeY, 0,
            meiaLargPeX, meiaLargPeY, alturaPe);

    combine(context, id + "uniao", parede, [pe], []);

    var cortes = [
            cylinderOnAxis(context, id + "boreEixo", vector(0, 0, d.alturaEixoX), "X",
                    c.boreEixo, 2 * meiaEspParede + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoPeA", vector(0, offsetFuroPe, alturaPe / 2), "Z",
                    c.passagem, alturaPe + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoPeB", vector(0, -offsetFuroPe, alturaPe / 2), "Z",
                    c.passagem, alturaPe + 2 * MARGEM)
    ];

    if (d.travaSetScrew)
    {
        var z0 = d.alturaEixoX - MARGEM;
        var z1 = topoZ + MARGEM;
        cortes = append(cortes, cylinderOnAxis(context, id + "trava",
                    vector(0, 0, (z0 + z1) / 2), "Z", c.rosca, z1 - z0));
    }

    combine(context, id + "furos", parede, [], cortes);
    return parede;
}

// =====================================================================
// 4) MANCAL_MESA - segura as pontas dos 2 eixos Y; face de cima encosta
//    no MDF e e parafusada por baixo
// =====================================================================
function construirMancalMesa(context is Context, id is Id, d is map, c is map) returns Query
{
    // Face de colagem no MDF em Z=0. O eixo Y fica ABAIXO dela o bastante
    // para o MDF passar livre por cima do suporte de LM8UU (c.mesaAcima).
    var altura = c.mesaAbaixo + c.mesaAcima;
    var meiaLargX = d.espacoY / 2 + d.eixoD / 2 + d.parede;
    var profund = 20;
    var zEixo = -c.mesaAcima;

    var limite = d.espacoY / 2 - (d.eixoD / 2 + c.passagem / 2 + MARGEM);
    if (d.furoMesaOffset >= limite)
    {
        throw regenError("Offset do furo da mesa grande demais: o furo encosta no eixo Y. Reduza o offset ou aumente o espaco entre eixos Y.");
    }

    var corpo = makeBlock(context, id + "corpo",
            -meiaLargX, -profund / 2, -altura,
            meiaLargX, profund / 2, 0);

    // Eixo do fuso Y em Z local: parte do centro do eixo Y do mundo
    // (alturaEixoX + topoCarro + holderZ), sobe ate d.fusoYZ, e o resultado
    // e trazido para o referencial local desta peca (face de cola no MDF em
    // Z=0). FONTE UNICA de posicao do fuso: fusoYX / fusoYZ.
    var zFusoY = -(c.mesaAcima - (d.fusoYZ - d.alturaEixoX - c.topoCarro - c.holderZ));

    // Furos verticais dos parafusos do MDF: centrados na ALTURA da peca
    // (-altura/2), nao em zEixo - centrados em zEixo eles paravam antes da
    // face de cola (1,75 mm na versao correia, 5,25 mm com o 04 de 26,5).
    var cortes = [
            cylinderOnAxis(context, id + "boreA", vector(d.espacoY / 2, 0, zEixo), "Y",
                    c.boreEixo, profund + 2 * MARGEM),
            cylinderOnAxis(context, id + "boreB", vector(-d.espacoY / 2, 0, zEixo), "Y",
                    c.boreEixo, profund + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoMesaA", vector(d.furoMesaOffset, 0, -altura / 2), "Z",
                    c.passagem, altura + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoMesaB", vector(-d.furoMesaOffset, 0, -altura / 2), "Z",
                    c.passagem, altura + 2 * MARGEM),
            // furo passante do fuso do Y - o acoplador (diametro 20) fica do
            // lado de fora e nao passa por aqui, so a haste do fuso (diametro
            // 8) atravessa
            cylinderOnAxis(context, id + "furoFusoY", vector(d.fusoYX, 0, zFusoY), "Y",
                    10, profund + 2 * MARGEM)
    ];

    // 4 furos de rosca M4 (diametro 3,3, fixo) para o suporte do motor Y
    // (peca 10), gerados por laco sobre FUROS_SUPORTE_MOTOR_Y - fonte unica
    // das posicoes, sem literais repetidos.
    var iSup = 0;
    for (var furo in FUROS_SUPORTE_MOTOR_Y)
    {
        cortes = append(cortes, cylinderOnAxis(context, id + ("furoSuporteMotorY" ~ iSup),
                    vector(d.fusoYX + furo[0], 0, zFusoY + furo[1]), "Y",
                    MOTOR_Y_ROSCA_M4, profund + 2 * MARGEM));
        iSup += 1;
    }

    if (d.travaSetScrew)
    {
        var z0 = -altura - MARGEM;
        var z1 = zEixo + MARGEM;
        for (var s in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("travaMesa" ~ s),
                        vector(s * d.espacoY / 2, 0, (z0 + z1) / 2), "Z", c.rosca, z1 - z0));
        }
    }

    combine(context, id + "furos", corpo, [], cortes);
    return corpo;
}

// =====================================================================
// FEATURE - gera o jogo completo, lado a lado, pronto para exportar
// =====================================================================
annotation { "Feature Type Name" : "Mesa XY - jogo de pecas" }
export const mesaXyPecas = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Parafuso de fixacao" }
        definition.parafuso is ParafusoMesaXy;

        annotation { "Name" : "Diametro do eixo liso" }
        isLength(definition.eixoD, { (millimeter) : [3, 8, 20] } as LengthBoundSpec);

        annotation { "Name" : "Folga do eixo no mancal (aperto)" }
        isLength(definition.folgaEixoFixo, { (millimeter) : [0, 0.1, 1] } as LengthBoundSpec);

        annotation { "Name" : "LM8UU - diametro externo" }
        isLength(definition.lmD, { (millimeter) : [5, 15, 40] } as LengthBoundSpec);

        annotation { "Name" : "LM8UU - comprimento" }
        isLength(definition.lmL, { (millimeter) : [5, 24, 60] } as LengthBoundSpec);

        annotation { "Name" : "LM8UU - folga no alojamento" }
        isLength(definition.folgaLm, { (millimeter) : [0, 0.1, 1] } as LengthBoundSpec);

        annotation { "Name" : "LM8UU - vao entre os dois" }
        isLength(definition.lmGap, { (millimeter) : [0, 2, 20] } as LengthBoundSpec);

        annotation { "Name" : "LM8UU por eixo" }
        isInteger(definition.qtdLm, { (unitless) : [1, 2, 3] } as IntegerBoundSpec);

        annotation { "Name" : "Espessura de parede" }
        isLength(definition.parede, { (millimeter) : [1, 3, 15] } as LengthBoundSpec);

        annotation { "Name" : "Eixos X - distancia entre centros" }
        isLength(definition.espacoX, { (millimeter) : [20, 120, 500] } as LengthBoundSpec);

        annotation { "Name" : "Eixos Y - distancia entre centros" }
        isLength(definition.espacoY, { (millimeter) : [20, 100, 500] } as LengthBoundSpec);

        annotation { "Name" : "Altura do eixo X acima da base" }
        isLength(definition.alturaEixoX, { (millimeter) : [5, 20, 200] } as LengthBoundSpec);

        annotation { "Name" : "Espessura da placa do carro" }
        isLength(definition.placaEsp, { (millimeter) : [2, 8, 30] } as LengthBoundSpec);

        annotation { "Name" : "Suporte - vao de aperto entre as metades" }
        isLength(definition.gapAperto, { (millimeter) : [0.1, 0.4, 3] } as LengthBoundSpec);

        annotation { "Name" : "Suporte - comprimento das abas do parafuso" }
        isLength(definition.padY, { (millimeter) : [8, 16, 60] } as LengthBoundSpec);

        annotation { "Name" : "Folga entre o MDF e o suporte de LM8UU" }
        isLength(definition.folgaMesa, { (millimeter) : [0.5, 2, 20] } as LengthBoundSpec);

        annotation { "Name" : "Mancal da mesa - offset do furo" }
        isLength(definition.furoMesaOffset, { (millimeter) : [5, 30, 300] } as LengthBoundSpec);

        annotation { "Name" : "Espaco entre pecas no layout" }
        isLength(definition.espacoLayout, { (millimeter) : [0, 20, 200] } as LengthBoundSpec);

        annotation { "Name" : "Correia Y - altura da linha da correia" }
        isLength(definition.correiaZ, { (millimeter) : [25, 37, 60] } as LengthBoundSpec);

        annotation { "Name" : "Correia Y - distancia do centro a cada polia" }
        isLength(definition.poliaY, { (millimeter) : [50, 170, 400] } as LengthBoundSpec);

        annotation { "Name" : "Gerar ponte + grampo da correia do Y", "Default" : true }
        definition.fazPonteY is boolean;

        annotation { "Name" : "Referencia - comprimento dos eixos lisos" }
        isLength(definition.eixoComp, { (millimeter) : [100, 400, 1500] } as LengthBoundSpec);

        annotation { "Name" : "Referencia - comprimento do fuso" }
        isLength(definition.fusoComp, { (millimeter) : [100, 300, 1500] } as LengthBoundSpec);

        annotation { "Name" : "Gerar SO os corpos de referencia (eixos, LM8UU, fuso)", "Default" : false }
        definition.soReferencias is boolean;

        annotation { "Name" : "Base MDF - espessura" }
        isLength(definition.baseEsp, { (millimeter) : [6, 15, 30] } as LengthBoundSpec);

        annotation { "Name" : "Base - distancia do centro a cada mancal 03" }
        isLength(definition.suporteEixoX, { (millimeter) : [50, 190, 600] } as LengthBoundSpec);

        annotation { "Name" : "Base - X do suporte do motor" }
        isLength(definition.motorX, { (millimeter) : [50, 210, 700] } as LengthBoundSpec);

        annotation { "Name" : "Base - X do mancal da ponta do fuso (negativo)" }
        isLength(definition.mancalFusoX, { (millimeter) : [-700, -90, 0] } as LengthBoundSpec);

        annotation { "Name" : "Mesa MDF - largura (X)" }
        isLength(definition.mesaX, { (millimeter) : [100, 300, 1200] } as LengthBoundSpec);

        annotation { "Name" : "Mesa MDF - comprimento (Y)" }
        isLength(definition.mesaY, { (millimeter) : [100, 410, 1200] } as LengthBoundSpec);

        annotation { "Name" : "Mesa MDF - espessura" }
        isLength(definition.mesaEsp, { (millimeter) : [6, 15, 30] } as LengthBoundSpec);

        annotation { "Name" : "Mesa MDF - distancia do centro a cada mancal 04" }
        isLength(definition.mesaFuroY, { (millimeter) : [50, 190, 600] } as LengthBoundSpec);

        annotation { "Name" : "Mesa MDF - pre-furo do parafuso" }
        isLength(definition.preFuroMdf, { (millimeter) : [1.5, 3.2, 8] } as LengthBoundSpec);

        annotation { "Name" : "Fuso - distancia do eixo do fuso ao centro (Y)" }
        isLength(definition.fusoY, { (millimeter) : [40, 90, 300] } as LengthBoundSpec);

        annotation { "Name" : "Fuso - altura do eixo do fuso acima da base" }
        isLength(definition.fusoZ, { (millimeter) : [10, 21, 100] } as LengthBoundSpec);

        annotation { "Name" : "Fuso Y - X do eixo do fuso (corredor entre os carros)" }
        isLength(definition.fusoYX, { (millimeter) : [-50, 0, 50] } as LengthBoundSpec);

        annotation { "Name" : "Fuso Y - altura do eixo do fuso acima da base" }
        isLength(definition.fusoYZ, { (millimeter) : [30, 47.5, 80] } as LengthBoundSpec);

        annotation { "Name" : "Fuso Y - Y da ponta do lado do motor (negativo)" }
        isLength(definition.fusoYPonta, { (millimeter) : [-400, -219, 0] } as LengthBoundSpec);

        annotation { "Name" : "Fuso Y - comprimento util" }
        isLength(definition.fusoYComp, { (millimeter) : [100, 300, 600] } as LengthBoundSpec);

        annotation { "Name" : "Espessura das paredes de flange (aba, motor, mancal)" }
        isLength(definition.flangeEsp, { (millimeter) : [3, 6, 15] } as LengthBoundSpec);

        annotation { "Name" : "Furo de trava (set screw) nos mancais", "Default" : false }
        definition.travaSetScrew is boolean;

        annotation { "Name" : "Gerar carro", "Default" : true }
        definition.fazCarro is boolean;

        annotation { "Name" : "Gerar suporte de LM8UU", "Default" : true }
        definition.fazSuporte is boolean;

        annotation { "Name" : "Gerar mancal da base", "Default" : true }
        definition.fazMancalBase is boolean;

        annotation { "Name" : "Gerar mancal da mesa", "Default" : true }
        definition.fazMancalMesa is boolean;

        annotation { "Name" : "Gerar carro com aba da castanha (o que o fuso puxa)", "Default" : true }
        definition.fazCarroFuso is boolean;

        annotation { "Name" : "Gerar suporte do motor NEMA17", "Default" : true }
        definition.fazSuporteMotor is boolean;

        annotation { "Name" : "Gerar mancal da ponta do fuso (608)", "Default" : true }
        definition.fazMancalFuso is boolean;

        annotation { "Name" : "Gerar suporte do motor Y (10)", "Default" : true }
        definition.fazSuporteMotorY is boolean;

        annotation { "Name" : "Gerar mancal do fuso Y (11)", "Default" : true }
        definition.fazMancalFusoY is boolean;

        annotation { "Name" : "Gerar a mesa de MDF (corte, nao impressao)", "Default" : true }
        definition.fazMesaMdf is boolean;

        annotation { "Name" : "Gerar a base de MDF / gabarito de furacao", "Default" : true }
        definition.fazBaseMdf is boolean;
    }
    {
        // unidade some aqui: daqui pra frente e tudo numero puro em mm
        var d = {
            "parafuso" : definition.parafuso,
            "eixoD" : definition.eixoD / millimeter,
            "folgaEixoFixo" : definition.folgaEixoFixo / millimeter,
            "lmD" : definition.lmD / millimeter,
            "lmL" : definition.lmL / millimeter,
            "folgaLm" : definition.folgaLm / millimeter,
            "lmGap" : definition.lmGap / millimeter,
            "qtdLm" : definition.qtdLm,
            "parede" : definition.parede / millimeter,
            "espacoX" : definition.espacoX / millimeter,
            "espacoY" : definition.espacoY / millimeter,
            "alturaEixoX" : definition.alturaEixoX / millimeter,
            "placaEsp" : definition.placaEsp / millimeter,
            "folgaMesa" : definition.folgaMesa / millimeter,
            "gapAperto" : definition.gapAperto / millimeter,
            "padY" : definition.padY / millimeter,
            "furoMesaOffset" : definition.furoMesaOffset / millimeter,
            "correiaZ" : definition.correiaZ / millimeter,
            "poliaY" : definition.poliaY / millimeter,
            "eixoComp" : definition.eixoComp / millimeter,
            "fusoComp" : definition.fusoComp / millimeter,
            "baseEsp" : definition.baseEsp / millimeter,
            "suporteEixoX" : definition.suporteEixoX / millimeter,
            "motorX" : definition.motorX / millimeter,
            "mancalFusoX" : definition.mancalFusoX / millimeter,
            "mesaX" : definition.mesaX / millimeter,
            "mesaY" : definition.mesaY / millimeter,
            "mesaEsp" : definition.mesaEsp / millimeter,
            "mesaFuroY" : definition.mesaFuroY / millimeter,
            "preFuroMdf" : definition.preFuroMdf / millimeter,
            "fusoY" : definition.fusoY / millimeter,
            "fusoZ" : definition.fusoZ / millimeter,
            "fusoYX" : definition.fusoYX / millimeter,
            "fusoYZ" : definition.fusoYZ / millimeter,
            "fusoYPonta" : definition.fusoYPonta / millimeter,
            "fusoYComp" : definition.fusoYComp / millimeter,
            "flangeEsp" : definition.flangeEsp / millimeter,
            "travaSetScrew" : definition.travaSetScrew
        };
        var c = cotas(d);
        var pal = paleta();

        if (definition.soReferencias)
        {
            construirReferencias(context, id + "ref", d, c);
            return;
        }

        // layout: cada peca ocupa sua faixa em X, na ordem da montagem
        // ----------------------------------------------------------------
        // VAGAS FIXAS DO LAYOUT (mm em X, centro de cada peca)
        //
        // Cada peca tem a sua vaga. NAO usar cursor corrido: se uma peca
        // entra ou sai, o cursor empurra todas as seguintes e as instancias
        // da montagem passam a apontar para o lugar errado - ja aconteceu,
        // e as duas chapas de MDF sairam 90 mm fora.
        // Para acrescentar peca, use uma vaga NOVA no fim, nunca no meio.
        // ----------------------------------------------------------------
        const VAGA = {
            "carro" : 0,
            "carroFuso" : 120,
            "suporteBase" : 240,
            "suporteTampa" : 310,
            "mancalBase" : 380,
            "mancalMesa" : 460,
            "suporteMotor" : 600,
            "mancalFuso" : 660,
            "ponteY" : 730,
            "mesaMdf" : 900,
            "baseMdf" : 1300,
            "suporteMotorY" : 1650,
            "mancalFusoY" : 1760
        };

        if (definition.fazCarro)
        {
            var carro = construirCarro(context, id + "carro", d, c, false);
            moveBody(context, id + "posCarro", carro, VAGA.carro, 0, 0);
            nomear(context, carro, "01_Carro_simples_x1", pal.carro);
        }

        if (definition.fazCarroFuso)
        {
            var carroFuso = construirCarro(context, id + "carroFuso", d, c, true);
            moveBody(context, id + "posCarroFuso", carroFuso, VAGA.carroFuso, 0, 0);
            nomear(context, carroFuso, "01B_Carro_com_aba_castanha_x1", pal.carro);
        }

        if (definition.fazSuporte)
        {
            var base = construirSuporteBase(context, id + "suporteBase", d, c);
            moveBody(context, id + "posSuporteBase", base, VAGA.suporteBase, 0, 0);
            nomear(context, base, "02A_Suporte_base_x2", pal.suporte);

            var tampa = construirSuporteTampa(context, id + "suporteTampa", d, c);
            moveBody(context, id + "posSuporteTampa", tampa, VAGA.suporteTampa, 0, 0);
            nomear(context, tampa, "02B_Suporte_tampa_x2", pal.suporte);
        }

        if (definition.fazMancalBase)
        {
            var mancalBase = construirMancalBase(context, id + "mancalBase", d, c);
            moveBody(context, id + "posMancalBase", mancalBase, VAGA.mancalBase, 0, 0);
            nomear(context, mancalBase, "03_Mancal_base_eixo_X_x4", pal.mancalBase);
        }

        if (definition.fazMancalMesa)
        {
            var mancalMesa = construirMancalMesa(context, id + "mancalMesa", d, c);
            // sobe para Z>=0: a peca e modelada com a face de colagem no MDF em Z=0
            moveBody(context, id + "posMancalMesa", mancalMesa, VAGA.mancalMesa, 0, c.mesaAbaixo + c.mesaAcima);
            nomear(context, mancalMesa, "04_Mancal_mesa_eixo_Y_x2", pal.mancalMesa);
        }

        if (definition.fazSuporteMotor)
        {
            // o pe do 05 ocupa de (motorX - PE_MOTOR) ate motorX; o pe do
            // mancal 03 termina em suporteEixoX + 10. Se invadir, e colisao
            // real na montagem - ja aconteceu uma vez.
            if (d.motorX - PE_MOTOR < d.suporteEixoX + 10 + 5)
            {
                throw regenError("O pe do suporte do motor bate no pe do mancal da base. Aumente o X do suporte do motor.");
            }
            var supMotor = construirSuporteMotor(context, id + "supMotor", d, c);
            moveBody(context, id + "posSupMotor", supMotor, VAGA.suporteMotor, 0, 0);
            nomear(context, supMotor, "05_Suporte_motor_NEMA17_x1", pal.motor);
        }

        if (definition.fazMancalFuso)
        {
            var mancalFuso = construirMancalFuso(context, id + "mancalFuso", d, c);
            moveBody(context, id + "posMancalFuso", mancalFuso, VAGA.mancalFuso, 0, 0);
            nomear(context, mancalFuso, "06_Mancal_ponta_fuso_608_x1", pal.mancalFuso);
        }

        if (definition.fazPonteY)
        {
            var ponteY = construirPonteY(context, id + "ponteY", d, c);
            moveBody(context, id + "posPonteY", ponteY, VAGA.ponteY, 0, -(d.alturaEixoX + c.topoCarro));
            nomear(context, ponteY, "09_Berco_castanha_Y_x1", pal.suporte);
        }

        if (definition.fazSuporteMotorY)
        {
            // mesma derivacao de construirSuporteMotorY (face do 04 / ponta
            // do eixo do motor) - so para achar o centro em Y e o fundo em Z
            // que trazem a peca para a vaga do layout.
            var yFace04SupY = -(d.mesaFuroY + MANCAL_04_MEIA_PROFUND);           // -200
            var yFaceMotorSupY = yFaceMotorY(d);                                 // -243.5
            var suporteMotorY = construirSuporteMotorY(context, id + "suporteMotorY", d, c);
            moveBody(context, id + "posSuporteMotorY", suporteMotorY, VAGA.suporteMotorY,
                    -(yFaceMotorSupY + yFace04SupY) / 2, -(d.fusoYZ - NEMA17_FLANGE / 2));
            nomear(context, suporteMotorY, "10_Suporte_motor_Y_x1", pal.suporte);
        }

        if (definition.fazMancalFusoY)
        {
            // mesma derivacao de construirMancalFusoY (centro do 608) - so
            // para centrar a peca em Y na vaga do layout.
            var yCentro608VAGA = yCentro608Y(d);                                    // 126
            var mancalFusoY = construirMancalFusoY(context, id + "mancalFusoY", d, c);
            moveBody(context, id + "posMancalFusoY", mancalFusoY, VAGA.mancalFusoY,
                    -yCentro608VAGA, -(d.fusoYZ - MANCAL_FUSO_Y_MEIO_CORPO));
            nomear(context, mancalFusoY, "11_Mancal_fuso_Y_608_x1", pal.mancalFuso);
        }

        if (definition.fazMesaMdf)
        {
            var mesa = construirMesaMdf(context, id + "mesaMdf", d, c);
            moveBody(context, id + "posMesaMdf", mesa, VAGA.mesaMdf, 0, 0);
            nomear(context, mesa, "07_Mesa_MDF_CORTAR_nao_imprimir_x1", pal.mdfMesa);
        }

        if (definition.fazBaseMdf)
        {
            var base2 = construirBaseMdf(context, id + "baseMdf", d, c);
            moveBody(context, id + "posBaseMdf", base2, VAGA.baseMdf, 0, 0);
            nomear(context, base2, "08_Base_MDF_gabarito_furacao_x1", pal.mdfBase);
        }
    });
