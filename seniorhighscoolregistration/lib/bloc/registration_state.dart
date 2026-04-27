// lib/bloc/registration_state.dart

import 'package:equatable/equatable.dart';
import '../data/models/registration_model.dart';

abstract class RegistrationState extends Equatable {
  const RegistrationState();

  @override
  List<Object?> get props => [];
}

class RegistrationInitial extends RegistrationState {
  const RegistrationInitial();
}

class RegistrationLoading extends RegistrationState {
  const RegistrationLoading();
}

class RegistrationsLoaded extends RegistrationState {
  final List<RegistrationModel> registrations;
  final String searchQuery;

  const RegistrationsLoaded(this.registrations, {this.searchQuery = ''});

  @override
  List<Object?> get props => [registrations, searchQuery];
}

class RegistrationSubmitting extends RegistrationState {
  const RegistrationSubmitting();
}

class RegistrationSuccess extends RegistrationState {
  final String message;
  final List<RegistrationModel> registrations;

  const RegistrationSuccess({
    required this.message,
    required this.registrations,
  });

  @override
  List<Object?> get props => [message, registrations];
}

class RegistrationError extends RegistrationState {
  final String message;

  const RegistrationError(this.message);

  @override
  List<Object?> get props => [message];
}

class DocumentPicked extends RegistrationState {
  final String filePath;
  final String fileName;

  const DocumentPicked({required this.filePath, required this.fileName});

  @override
  List<Object?> get props => [filePath, fileName];
}

class DocumentCleared extends RegistrationState {
  const DocumentCleared();
}

class DashboardStatsLoaded extends RegistrationState {
  final List<RegistrationModel> registrations;
  final Map<String, int> strandCounts;
  final int totalCount;

  const DashboardStatsLoaded({
    required this.registrations,
    required this.strandCounts,
    required this.totalCount,
  });

  @override
  List<Object?> get props => [registrations, strandCounts, totalCount];
}
