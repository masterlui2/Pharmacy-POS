// lib/bloc/registration_bloc.dart

import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:file_picker/file_picker.dart';

import '../data/repositories/registration_repository.dart';
import 'registration_event.dart';
import 'registration_state.dart';

class RegistrationBloc extends Bloc<RegistrationEvent, RegistrationState> {
  final RegistrationRepository _repository;

  String? _pickedFilePath;
  String? _pickedFileName;

  RegistrationBloc({required RegistrationRepository repository})
    : _repository = repository,
      super(const RegistrationInitial()) {
    on<LoadRegistrations>(_onLoadRegistrations);
    on<SearchRegistrations>(_onSearchRegistrations);
    on<SubmitRegistration>(_onSubmitRegistration);
    on<UpdateRegistration>(_onUpdateRegistration);
    on<DeleteRegistration>(_onDeleteRegistration);
    on<PickDocumentFile>(_onPickDocumentFile);
    on<ClearPickedDocument>(_onClearPickedDocument);
    on<LoadDashboardStats>(_onLoadDashboardStats);
  }

  Future<void> _onLoadRegistrations(
    LoadRegistrations event,
    Emitter<RegistrationState> emit,
  ) async {
    emit(const RegistrationLoading());
    try {
      final registrations = await _repository.fetchAllRegistrations();
      emit(RegistrationsLoaded(registrations));
    } catch (e) {
      emit(RegistrationError('Failed to load registrations: $e'));
    }
  }

  Future<void> _onSearchRegistrations(
    SearchRegistrations event,
    Emitter<RegistrationState> emit,
  ) async {
    try {
      final registrations = event.query.trim().isEmpty
          ? await _repository.fetchAllRegistrations()
          : await _repository.searchRegistrations(event.query.trim());
      emit(RegistrationsLoaded(registrations, searchQuery: event.query));
    } catch (e) {
      emit(RegistrationError('Search failed: $e'));
    }
  }

  Future<void> _onSubmitRegistration(
    SubmitRegistration event,
    Emitter<RegistrationState> emit,
  ) async {
    emit(const RegistrationSubmitting());
    try {
      final registrationWithDoc = event.registration.copyWith(
        documentPath: _pickedFilePath,
        documentName: _pickedFileName,
      );
      await _repository.addRegistration(registrationWithDoc);
      _pickedFilePath = null;
      _pickedFileName = null;

      final registrations = await _repository.fetchAllRegistrations();
      emit(
        RegistrationSuccess(
          message: 'Student registered successfully!',
          registrations: registrations,
        ),
      );
    } catch (e) {
      emit(RegistrationError('Failed to submit registration: $e'));
    }
  }

  Future<void> _onUpdateRegistration(
    UpdateRegistration event,
    Emitter<RegistrationState> emit,
  ) async {
    emit(const RegistrationSubmitting());
    try {
      final registrationWithDoc = event.registration.copyWith(
        documentPath: _pickedFilePath ?? event.registration.documentPath,
        documentName: _pickedFileName ?? event.registration.documentName,
      );
      await _repository.updateRegistration(registrationWithDoc);
      _pickedFilePath = null;
      _pickedFileName = null;

      final registrations = await _repository.fetchAllRegistrations();
      emit(
        RegistrationSuccess(
          message: 'Registration updated successfully!',
          registrations: registrations,
        ),
      );
    } catch (e) {
      emit(RegistrationError('Failed to update registration: $e'));
    }
  }

  Future<void> _onDeleteRegistration(
    DeleteRegistration event,
    Emitter<RegistrationState> emit,
  ) async {
    try {
      await _repository.removeRegistration(event.id);
      final registrations = await _repository.fetchAllRegistrations();
      emit(
        RegistrationSuccess(
          message: 'Registration deleted.',
          registrations: registrations,
        ),
      );
    } catch (e) {
      emit(RegistrationError('Failed to delete registration: $e'));
    }
  }

  Future<void> _onPickDocumentFile(
    PickDocumentFile event,
    Emitter<RegistrationState> emit,
  ) async {
    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf', 'jpg', 'jpeg', 'png'],
        allowMultiple: false,
      );
      if (result != null && result.files.isNotEmpty) {
        final file = result.files.first;
        _pickedFilePath = file.path;
        _pickedFileName = file.name;
        emit(DocumentPicked(filePath: file.path ?? '', fileName: file.name));
      }
    } catch (e) {
      emit(RegistrationError('Failed to pick file: $e'));
    }
  }

  void _onClearPickedDocument(
    ClearPickedDocument event,
    Emitter<RegistrationState> emit,
  ) {
    _pickedFilePath = null;
    _pickedFileName = null;
    emit(const DocumentCleared());
  }

  Future<void> _onLoadDashboardStats(
    LoadDashboardStats event,
    Emitter<RegistrationState> emit,
  ) async {
    emit(const RegistrationLoading());
    try {
      final registrations = await _repository.fetchAllRegistrations();
      final strandCounts = await _repository.fetchStrandCounts();
      emit(
        DashboardStatsLoaded(
          registrations: registrations,
          strandCounts: strandCounts,
          totalCount: registrations.length,
        ),
      );
    } catch (e) {
      emit(RegistrationError('Failed to load stats: $e'));
    }
  }

  String? get pickedFilePath => _pickedFilePath;
  String? get pickedFileName => _pickedFileName;
}
