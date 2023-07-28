grammar HolyJava;

program: line* EOF;

line: statement;

statement: (varReassignment | varAssignment | varDeclaration);

varReassignment: IDENTIFIER '=' expression ';';
varAssignment: varParameter '=' expression ';';
varDeclaration: varParameter ';';
varParameter: varType IDENTIFIER;
varType: PRIMATIVE_INT | PRIMATIVE_STRING | PRIMATIVE_FLOAT | PRIMATIVE_BOOL;

expression
: constant								#constantExpression
| IDENTIFIER							#identifierExpression
| expression multOp expression          #multiplicativeExpression
| expression comparisonOps expression   #comparisonExpression
| expression addOp expression	        #additiveExpression
;

multOp: '*' | '/';
addOp: '+' | '-';
comparisonOps: '<' | '>' | '<=' | '>=' | '==' | '!=';
constant: INT | STRING | FLOAT | BOOL | NULL;

PRIMATIVE_INT: 'int';
PRIMATIVE_FLOAT: 'float';
PRIMATIVE_STRING: 'string';
PRIMATIVE_BOOL: 'bool';

INT: '-'? [0-9]+;
FLOAT: '-'? [0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
NULL: 'null';

block: '{' line* '}';
IDENTIFIER: [a-zA-Z@_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;