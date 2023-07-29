grammar HolyJava;

program: line* EOF;

line: statement;

statement: (varKeywords | funcKeywords | returnStatement);

varKeywords: (varReassignment | varAssignment | varDeclaration);
funcKeywords: (funcDefinition | funcCall);

returnStatement: 'return' expression ';';

varReassignment: IDENTIFIER '=' expression ';';
varAssignment: varParameter '=' expression ';';
varDeclaration: varParameter ';';
varParameter: varType IDENTIFIER;
varType: PRIMATIVE_INT | PRIMATIVE_STRING | PRIMATIVE_FLOAT | PRIMATIVE_BOOL;

funcDefinition: 'func' IDENTIFIER '(' (varDeclaration (',' varDeclaration)*)? ')' ('->' varType)? block;
funcCall: IDENTIFIER '(' (expression (',' expression)*)? ')' ';';

expression
: funcCall								#funcCallExpression
| IDENTIFIER							#identifierExpression
| expression multOp expression          #multiplicativeExpression
| expression comparisonOps expression   #comparisonExpression
| expression addOp expression	        #additiveExpression
| constant								#constantExpression
;

multOp: '*' | '/';
addOp: '+' | '-';
comparisonOps: '<' | '>' | '<=' | '>=' | '==' | '!=';
constant: INT | STRING | FLOAT | BOOL;

PRIMATIVE_INT: 'int';
PRIMATIVE_FLOAT: 'float';
PRIMATIVE_STRING: 'string';
PRIMATIVE_BOOL: 'bool';

INT: (NEG_MOD NEG_MOD)? [0-9]+;
FLOAT: (NEG_MOD NEG_MOD)? [0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
NULL: 'null';

NEG_MOD: '-';

block: '{' line* '}';
IDENTIFIER: [a-zA-Z@_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;