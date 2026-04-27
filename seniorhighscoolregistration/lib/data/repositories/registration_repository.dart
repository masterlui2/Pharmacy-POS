// lib/data/repositories/registration_repository.dart

import '../database_helper.dart';
import '../models/registration_model.dart';

class RegistrationRepository {
  final DatabaseHelper _dbHelper;

  RegistrationRepository({DatabaseHelper? dbHelper})
      : _dbHelper = dbHelper ?? DatabaseHelper();

  Future<int> addRegistration(RegistrationModel registration) async {
    return await _dbHelper.insertRegistration(registration);
  }

  Future<List<RegistrationModel>> fetchAllRegistrations() async {
    return await _dbHelper.getAllRegistrations();
  }

  Future<int> updateRegistration(RegistrationModel registration) async {
    return await _dbHelper.updateRegistration(registration);
  }

  Future<List<RegistrationModel>> searchRegistrations(String query) async {
    return await _dbHelper.searchRegistrations(query);
  }

  Future<Map<String, int>> fetchStrandCounts() async {
    return await _dbHelper.getStrandCounts();
  }

  Future<int> removeRegistration(int id) async {
    return await _dbHelper.deleteRegistration(id);
  }
}