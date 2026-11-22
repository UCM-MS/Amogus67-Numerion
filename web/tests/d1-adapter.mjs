import {DatabaseSync} from 'node:sqlite';
import {readdirSync,readFileSync} from 'node:fs';

// Matches the D1 operations used by the app, with actual SQLite transactions.
export function createTestD1({file=':memory:',migrate=true}={}){
  const db=new DatabaseSync(file);
  db.exec('PRAGMA foreign_keys=ON');
  if(migrate){
    db.exec('CREATE TABLE IF NOT EXISTS local_migrations(name TEXT PRIMARY KEY)');
    const folder=new URL('../drizzle/',import.meta.url);
    for(const name of readdirSync(folder).filter(x=>x.endsWith('.sql')).sort()){
      if(db.prepare('SELECT name FROM local_migrations WHERE name=?').get(name))continue;
      db.exec('BEGIN');
      try {db.exec(readFileSync(new URL(name,folder),'utf8'));db.prepare('INSERT INTO local_migrations VALUES(?)').run(name);db.exec('COMMIT');}
      catch(e){db.exec('ROLLBACK');throw e;}
    }
  }
  class Statement{
    constructor(sql,args=[]){this.sql=sql;this.args=args;}
    bind(...args){return new Statement(this.sql,args);}
    async first(column){const row=db.prepare(this.sql).get(...this.args)||null;return column?row?.[column]??null:row;}
    async all(){return {success:true,results:db.prepare(this.sql).all(...this.args),meta:{}};}
    async run(){const result=db.prepare(this.sql).run(...this.args);return {success:true,results:[],meta:{changes:Number(result.changes),last_row_id:Number(result.lastInsertRowid)}};}
    async raw(){return (await this.all()).results.map(Object.values);}
  }
  return {
    prepare(sql){return new Statement(sql);},
    async batch(statements){db.exec('BEGIN');try{const results=[];for(const s of statements){const r=db.prepare(s.sql).run(...s.args);results.push({success:true,results:[],meta:{changes:Number(r.changes),last_row_id:Number(r.lastInsertRowid)}});}db.exec('COMMIT');return results;}catch(e){db.exec('ROLLBACK');throw e;}},
    async exec(sql){db.exec(sql);return {count:1,duration:0};},
    close(){db.close();},database:db
  };
}
