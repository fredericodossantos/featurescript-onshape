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
const NEMA17_BOSS = 25;         // folga p/ o ressalto (22) COM barriga de impressao
const CASTANHA_BOSS = 10.2;     // diametro do corpo da castanha T8
const CASTANHA_PCD = 16;        // circulo dos 4 furos da castanha
const CASTANHA_FURO = 3.5;      // furos da castanha (M3)
const ROL608_OD = 22.1;         // 608ZZ: externo 22 + 0,1 de ajuste
const ROL608_W = 7;             // 608ZZ: largura
const ROL608_ENCOSTO = 16;      // furo de encosto atras do rolamento
const PE_MOTOR = 25;            // comprimento do pe do suporte do motor
const PE_MANCAL_FUSO = 20;      // comprimento do pe do mancal da ponta
// Correia GT2 do kit: polia de 20 dentes, correia de 6 mm.
const GT2_DP = 12.73;           // diametro primitivo da polia 20d (20*2/pi)
const GT2_EIXO = 5.3;           // furo para o parafuso M5 que faz de eixo
const CORREIA_LARG = 6;         // largura da correia
const CORREIA_ESP = 1.5;        // espessura da correia com dente

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
    var base = makeBlock(context, id + "pe", -pe, -meia, 0, 0 + MARGEM, meia, 5);
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
                    vector(-pe / 2, sy * (meia - 6), 2.5), "Z",
                    c.passagem, 5 + 2 * MARGEM));
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

    // --- acionamento do Y: motor EM CIMA da chapa, polia livre num parafuso ---
    // O motor apoia na face de cima do MDF e o eixo desce pelo furo. Com 15 mm
    // de chapa sobram 8,5 mm de eixo embaixo - exatamente a parte dentada da
    // polia de 20 dentes. Nada do motor fica pendurado embaixo da mesa, e e
    // isso que preserva o curso em Y.
    cortes = append(cortes, cylinderOnAxis(context, id + "passaEixoMotorY",
                vector(0, d.poliaY, d.mesaEsp / 2), "Z", NEMA17_BOSS, d.mesaEsp + 2 * MARGEM));
    // rebaixo de 3 mm para a flange do motor: e o que abaixa a linha da
    // correia de 48 para 45 e deixa teto no grampo da ponte.
    cortes = append(cortes, cylinder(context, id + "rebaixoMotorY",
                vector(0, d.poliaY, d.mesaEsp - 3) * 1,
                vector(0, d.poliaY, d.mesaEsp + MARGEM) * 1, (NEMA17_FLANGE + 0.7) / 2));
    var k = 0;
    for (var sx3 in [-1, 1])
    {
        for (var sy3 in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("furoMotorY" ~ k),
                        vector(sx3 * NEMA17_PCD / 2, d.poliaY + sy3 * NEMA17_PCD / 2, d.mesaEsp / 2),
                        "Z", NEMA17_FURO, d.mesaEsp + 2 * MARGEM));
            k += 1;
        }
    }
    // polia livre: um parafuso M5 atravessa a chapa e serve de eixo
    cortes = append(cortes, cylinderOnAxis(context, id + "furoPoliaLivreY",
                vector(0, -d.poliaY, d.mesaEsp / 2), "Z", GT2_EIXO + 0.2,
                d.mesaEsp + 2 * MARGEM));

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

    // ---------------- acionamento: polias, correia, motores ----------------
    // Representacao so para ver folga e posicao. Medidas de catalogo:
    // polia GT2 20 dentes -> primitivo 12,73 e flange 16,5; correia 6 x 1,5.
    var zCorreia = d.correiaZ;
    var rPrim = GT2_DP / 2;
    var poliaZ0 = zCorreia - CORREIA_LARG / 2;          // base da parte dentada
    var poliaZ1 = zCorreia + CORREIA_LARG / 2;          // topo da parte dentada

    for (var sy2 in [-1, 1])
    {
        var yP = sy2 * d.poliaY;
        var lado = (sy2 > 0 ? "motor" : "livre");
        // parte dentada
        var pol = cylinder(context, id + ("polia" ~ sy2),
                vector(0, yP, poliaZ0) * 1, vector(0, yP, poliaZ1) * 1, GT2_DP / 2);
        // flanges
        var fl1 = cylinder(context, id + ("poliaFl1" ~ sy2),
                vector(0, yP, poliaZ0 - 1.5) * 1, vector(0, yP, poliaZ0) * 1, 8.25);
        var fl2 = cylinder(context, id + ("poliaFl2" ~ sy2),
                vector(0, yP, poliaZ1) * 1, vector(0, yP, poliaZ1 + 1.5) * 1, 8.25);
        combine(context, id + ("poliaUniao" ~ sy2), pol, [fl1, fl2], []);
        nomear(context, pol, "REF_polia_GT2_20d_" ~ lado ~ "_NAO_IMPRIMIR", pal.aluminio);

        // volta da correia em torno da polia (casca)
        var voltaFora = cylinder(context, id + ("voltaOut" ~ sy2),
                vector(0, yP, poliaZ0) * 1, vector(0, yP, poliaZ1) * 1, rPrim + CORREIA_ESP);
        var voltaDentro = cylinder(context, id + ("voltaIn" ~ sy2),
                vector(0, yP, poliaZ0 - MARGEM) * 1, vector(0, yP, poliaZ1 + MARGEM) * 1, rPrim);
        combine(context, id + ("volta" ~ sy2), voltaFora, [], [voltaDentro]);
        nomear(context, voltaFora, "REF_correia_volta_" ~ lado ~ "_NAO_IMPRIMIR", pal.borracha);
    }

    // os dois trechos retos da correia, tangentes ao primitivo das polias
    for (var sx5 in [-1, 1])
    {
        var trecho = makeBlock(context, id + ("correiaReta" ~ sx5),
                sx5 * rPrim - (sx5 > 0 ? 0 : CORREIA_ESP), -d.poliaY, poliaZ0,
                sx5 * rPrim + (sx5 > 0 ? CORREIA_ESP : 0), d.poliaY, poliaZ1);
        nomear(context, trecho, "REF_correia_trecho_" ~ (sx5 > 0 ? "A" : "B") ~ "_NAO_IMPRIMIR", pal.borracha);
    }

    // parafuso M5 que faz de eixo da polia livre
    var parafusoPolia = cylinder(context, id + "parafusoPolia",
            vector(0, -d.poliaY, poliaZ0 - 4) * 1,
            vector(0, -d.poliaY, d.alturaEixoX + c.topoCarro + c.holderZ + c.mesaAcima + d.mesaEsp + 4) * 1, 2.5);
    nomear(context, parafusoPolia, "REF_parafuso_M5_polia_livre_NAO_IMPRIMIR", pal.aco);

    // motor do Y: apoia na face de cima do MDF, rebaixado 3 mm, corpo para cima
    var zMdfTopo = d.alturaEixoX + c.topoCarro + c.holderZ + c.mesaAcima + d.mesaEsp;
    var motorY = makeBlock(context, id + "motorY",
            -NEMA17_FLANGE / 2, d.poliaY - NEMA17_FLANGE / 2, zMdfTopo - 3,
            NEMA17_FLANGE / 2, d.poliaY + NEMA17_FLANGE / 2, zMdfTopo - 3 + 38);
    nomear(context, motorY, "REF_motor_NEMA17_Y_NAO_IMPRIMIR", pal.motorCorpo);

    // motor do X: atras da parede do suporte 05
    var motorX = makeBlock(context, id + "motorX",
            d.motorX + d.flangeEsp, d.fusoY - NEMA17_FLANGE / 2, d.fusoZ - NEMA17_FLANGE / 2,
            d.motorX + d.flangeEsp + 38, d.fusoY + NEMA17_FLANGE / 2, d.fusoZ + NEMA17_FLANGE / 2);
    nomear(context, motorX, "REF_motor_NEMA17_X_NAO_IMPRIMIR", pal.motorCorpo);

    // acoplador entre o motor do X e o fuso
    var acoplador = cylinder(context, id + "acoplador",
            vector(xFusoPonta, d.fusoY, d.fusoZ) * 1,
            vector(d.motorX + d.flangeEsp, d.fusoY, d.fusoZ) * 1, 10);
    nomear(context, acoplador, "REF_acoplador_NAO_IMPRIMIR", pal.aluminio);
}


// =====================================================================
// 09) PONTE + GRAMPO DA CORREIA DO Y
//
//     Aparafusada nas faces INTERNAS dos dois suportes 02A (2 furos M4
//     em cada um, feitos com furadeira nas abas que ja existem). E o
//     ponto fixo contra o qual a correia puxa a mesa.
//
//     As duas pontas da correia entram nos dois rasgos e voltam dobradas
//     sobre si mesmas: dente contra dente, que e o jeito que segura sem
//     precisar de parafuso de aperto.
//     Os rasgos ficam na distancia das duas voltas da correia, que e o
//     diametro primitivo da polia.
// =====================================================================
function construirPonteY(context is Context, id is Id, d is map, c is map) returns Query
{
    var zCarro = d.alturaEixoX + c.topoCarro;          // topo do carro: 30,5
    var zFuroLado = zCarro + (c.holderZ - d.gapAperto / 2) / 2;   // meia altura do 02A
    var xFace = d.espacoY / 2 - c.holderX;             // face interna do 02A: 29
    var espPonta = 4;
    // O rasgo da correia precisa de teto: sem material em cima, a correia
    // salta fora. Por isso o topo fica 3 mm acima do rasgo - e e isso que
    // obriga a correia a correr em Z=45, nao mais alto.
    var zTopo = d.correiaZ + CORREIA_LARG / 2 + 0.3 + d.parede;

    // viga central, na altura da correia
    var viga = makeBlock(context, id + "viga",
            -(xFace - espPonta + MARGEM), -12, d.correiaZ - CORREIA_LARG / 2 - d.parede,
            xFace - espPonta + MARGEM, 12, zTopo);

    var adicoes = [];
    for (var sx in [-1, 1])
    {
        // placa de ponta que encosta na face interna do 02A
        adicoes = append(adicoes, makeBlock(context, id + ("ponta" ~ sx),
                    sx * (xFace - espPonta), -25, zCarro,
                    sx * xFace, 25, zTopo));
    }
    combine(context, id + "uniao", viga, adicoes, []);

    var cortes = [];
    // 4 furos de passagem para os M4 que entram nas abas do 02A
    var i = 0;
    for (var sx2 in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            cortes = append(cortes, cylinderOnAxis(context, id + ("furoLado" ~ i),
                        vector(sx2 * (xFace - espPonta / 2), sy * c.furoHolderY, zFuroLado),
                        "X", c.passagem, espPonta + 2 * MARGEM));
            i += 1;
        }
    }
    // 2 rasgos da correia (passam ao longo de Y), um por volta da correia
    for (var sx3 in [-1, 1])
    {
        var xr = sx3 * GT2_DP / 2;
        cortes = append(cortes, makeBlock(context, id + ("rasgo" ~ sx3),
                    xr - (2 * CORREIA_ESP + 0.6) / 2, -12 - MARGEM, d.correiaZ - CORREIA_LARG / 2 - 0.3,
                    xr + (2 * CORREIA_ESP + 0.6) / 2, 12 + MARGEM, d.correiaZ + CORREIA_LARG / 2 + 0.3));
    }

    combine(context, id + "furos", viga, [], cortes);
    return viga;
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
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            var x0 = sx * c.dExtAloj / 2;
            var x1 = sx * c.holderX;
            abas = append(abas, makeBlock(context, id + ("aba" ~ i),
                        min(x0, x1), sy * c.furoHolderY - d.padY / 2, z0,
                        max(x0, x1), sy * c.furoHolderY + d.padY / 2, z1));
            i += 1;
        }
    }
    return abas;
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

    var cortes = [
            cylinderOnAxis(context, id + "boreA", vector(d.espacoY / 2, 0, zEixo), "Y",
                    c.boreEixo, profund + 2 * MARGEM),
            cylinderOnAxis(context, id + "boreB", vector(-d.espacoY / 2, 0, zEixo), "Y",
                    c.boreEixo, profund + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoMesaA", vector(d.furoMesaOffset, 0, zEixo), "Z",
                    c.passagem, altura + 2 * MARGEM),
            cylinderOnAxis(context, id + "furoMesaB", vector(-d.furoMesaOffset, 0, zEixo), "Z",
                    c.passagem, altura + 2 * MARGEM)
    ];

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
            "baseMdf" : 1300
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
            nomear(context, ponteY, "09_Ponte_grampo_correia_Y_x1", pal.suporte);
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
