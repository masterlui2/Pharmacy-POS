// lib/data/database_helper.dart

import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';
import 'models/registration_model.dart';

class DatabaseHelper {
  static final DatabaseHelper _instance = DatabaseHelper._internal();
  static Database? _database;

  factory DatabaseHelper() => _instance;
  DatabaseHelper._internal();

  static const String _tableName = 'registrations';
  static const String _dbName = 'student_registration.db';
  static const int _dbVersion = 1;

  Future<Database> get database async {
    _database ??= await _initDatabase();
    return _database!;
  }

  Future<Database> _initDatabase() async {
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, _dbName);

    return await openDatabase(
      path,
      version: _dbVersion,
      onCreate: _onCreate,
    );
  }

  Future<void> _onCreate(Database db, int version) async {
    await db.execute('''
      CREATE TABLE $_tableName (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        staffName TEXT NOT NULL,
        studentName TEXT NOT NULL,
        date TEXT NOT NULL,
        strand TEXT NOT NULL,
        documentPath TEXT,
        documentName TEXT
      )
    ''');
  }

  Future<int> insertRegistration(RegistrationModel registration) async {
    final db = await database;
    return await db.insert(
      _tableName,
      registration.toMap(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<RegistrationModel>> getAllRegistrations() async {
    final db = await database;
    final maps = await db.query(
      _tableName,
      orderBy: 'id DESC',
    );
    return maps.map((map) => RegistrationModel.fromMap(map)).toList();
  }

  Future<int> updateRegistration(RegistrationModel registration) async {
    final db = await database;
    return await db.update(
      _tableName,
      registration.toMap(),
      where: 'id = ?',
      whereArgs: [registration.id],
    );
  }

  Future<List<RegistrationModel>> searchRegistrations(String query) async {
    final db = await database;
    final maps = await db.query(
      _tableName,
      where: 'studentName LIKE ? OR staffName LIKE ? OR strand LIKE ?',
      whereArgs: ['%$query%', '%$query%', '%$query%'],
      orderBy: 'id DESC',
    );
    return maps.map((map) => RegistrationModel.fromMap(map)).toList();
  }

  Future<Map<String, int>> getStrandCounts() async {
    final db = await database;
    final result = await db.rawQuery(
      'SELECT strand, COUNT(*) as count FROM $_tableName GROUP BY strand',
    );
    return {for (final row in result) row['strand'] as String: row['count'] as int};
  }

  Future<int> deleteRegistration(int id) async {
    final db = await database;
    return await db.delete(
      _tableName,
      where: 'id = ?',
      whereArgs: [id],
    );
  }

  Future<void> close() async {
    final db = _database;
    if (db != null) {
      await db.close();
      _database = null;
    }
  }
}